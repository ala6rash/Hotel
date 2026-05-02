using HotelBookingSaaS.API.Adapters.Implementations;
using HotelBookingSaaS.API.Adapters.Interfaces;
using HotelBookingSaaS.API.Data;
using HotelBookingSaaS.API.DTOs;
using HotelBookingSaaS.API.Enums;
using HotelBookingSaaS.API.Hubs;
using HotelBookingSaaS.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.SignalR;

namespace HotelBookingSaaS.API.Services;

public interface IBookingSyncService
{
    Task SyncHotelAsync(int hotelId, CancellationToken ct = default);
    Task SyncAllHotelsAsync(CancellationToken ct = default);
    Task SyncFromWebhookAsync(int hotelId, StandardBookingDto booking, CancellationToken ct = default);
}

/// <summary>
/// Universal sync service — pulls bookings from both PMS and OTAs (via Channex),
/// deduplicates, persists to the database, and pushes real-time updates via SignalR.
/// </summary>
public class BookingSyncService : IBookingSyncService
{
    private readonly ApplicationDbContext _db;
    private readonly IPmsAdapterFactory _pmsFactory;
    private readonly ChannexAdapter _channexAdapter;
    private readonly IHubContext<BookingHub> _hub;
    private readonly ILogger<BookingSyncService> _logger;

    public BookingSyncService(
        ApplicationDbContext db,
        IPmsAdapterFactory pmsFactory,
        ChannexAdapter channexAdapter,
        IHubContext<BookingHub> hub,
        ILogger<BookingSyncService> logger)
    {
        _db = db;
        _pmsFactory = pmsFactory;
        _channexAdapter = channexAdapter;
        _hub = hub;
        _logger = logger;
    }

    // ─── Public API ───────────────────────────────────────────────────────────

    public async Task SyncAllHotelsAsync(CancellationToken ct = default)
    {
        var hotels = await _db.Hotels
            .Where(h => h.IsActive)
            .ToListAsync(ct);

        _logger.LogInformation("Starting sync for {Count} active hotels", hotels.Count);

        var tasks = hotels.Select(h => SyncHotelSafeAsync(h.HotelId, ct));
        await Task.WhenAll(tasks);
    }

    public async Task SyncHotelAsync(int hotelId, CancellationToken ct = default)
    {
        var log = await StartSyncLogAsync(hotelId, null, null, ct);

        try
        {
            var hotel = await _db.Hotels
                .Include(h => h.PMSCredentials)
                .Include(h => h.OTACredentials)
                .FirstOrDefaultAsync(h => h.HotelId == hotelId, ct)
                ?? throw new InvalidOperationException($"Hotel {hotelId} not found");

            var fromDate = DateTime.UtcNow.Date.AddDays(-1);
            var toDate = DateTime.UtcNow.Date.AddDays(90);

            var allBookings = new List<StandardBookingDto>();

            // 1. Pull from PMS
            var pmsBookings = await PullFromPmsAsync(hotel, fromDate, toDate, ct);
            allBookings.AddRange(pmsBookings);
            _logger.LogInformation("Hotel {HotelId}: pulled {Count} bookings from PMS", hotelId, pmsBookings.Count);

            // 2. Pull from Channex (all connected OTAs)
            var otaBookings = await PullFromChannexAsync(hotel, fromDate, toDate, ct);
            allBookings.AddRange(otaBookings);
            _logger.LogInformation("Hotel {HotelId}: pulled {Count} bookings from OTAs via Channex", hotelId, otaBookings.Count);

            // 3. Upsert — no duplicates
            var (created, updated) = await UpsertBookingsAsync(hotelId, allBookings, ct);

            // 4. Notify via SignalR
            if (created > 0)
                await _hub.Clients.Group($"hotel_{hotelId}")
                    .SendAsync("SyncComplete", new
                    {
                        HotelId = hotelId,
                        Created = created,
                        Updated = updated,
                        Timestamp = DateTime.UtcNow
                    }, ct);

            await FinishSyncLogAsync(log, SyncStatus.Success,
                allBookings.Count, created, updated, 0, null, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sync failed for hotel {HotelId}", hotelId);
            await FinishSyncLogAsync(log, SyncStatus.Failed, 0, 0, 0, 0, ex.Message, ct);
            throw;
        }
    }

    public async Task SyncFromWebhookAsync(int hotelId, StandardBookingDto booking, CancellationToken ct = default)
    {
        _logger.LogInformation("Webhook sync for hotel {HotelId}, OTA booking {Id}",
            hotelId, booking.ExternalBookingId);

        var (created, updated) = await UpsertBookingsAsync(hotelId, new[] { booking }, ct);

        if (created > 0)
        {
            await CreateNotificationAsync(hotelId, booking, ct);
            await _hub.Clients.Group($"hotel_{hotelId}")
                .SendAsync("NewBooking", new
                {
                    HotelId = hotelId,
                    Booking = booking,
                    Timestamp = DateTime.UtcNow
                }, ct);
        }
    }

    // ─── Private helpers ──────────────────────────────────────────────────────

    private async Task SyncHotelSafeAsync(int hotelId, CancellationToken ct)
    {
        try { await SyncHotelAsync(hotelId, ct); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sync swallowed for hotel {HotelId}", hotelId);
        }
    }

    private async Task<List<StandardBookingDto>> PullFromPmsAsync(
        Hotel hotel, DateTime from, DateTime to, CancellationToken ct)
    {
        var credential = hotel.PMSCredentials
            .FirstOrDefault(c => c.IsActive && c.PmsType == hotel.PmsType);

        if (credential == null)
        {
            _logger.LogDebug("Hotel {HotelId}: no active PMS credential", hotel.HotelId);
            return new List<StandardBookingDto>();
        }

        try
        {
            var adapter = _pmsFactory.Create(credential);
            var bookings = await adapter.GetBookingsAsync(from, to, ct);
            return bookings.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Hotel {HotelId}: PMS pull failed", hotel.HotelId);
            return new List<StandardBookingDto>();
        }
    }

    private async Task<List<StandardBookingDto>> PullFromChannexAsync(
        Hotel hotel, DateTime from, DateTime to, CancellationToken ct)
    {
        var credential = hotel.OTACredentials
            .FirstOrDefault(c => c.IsActive && c.IsViaChannex);

        if (credential == null) return new List<StandardBookingDto>();

        try
        {
            _channexAdapter.Initialize(credential);
            var bookings = await _channexAdapter.GetBookingsAsync(from, to, ct);
            return bookings.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Hotel {HotelId}: Channex pull failed", hotel.HotelId);
            return new List<StandardBookingDto>();
        }
    }

    private async Task<(int created, int updated)> UpsertBookingsAsync(
        int hotelId, IEnumerable<StandardBookingDto> incoming, CancellationToken ct)
    {
        int created = 0, updated = 0;

        foreach (var dto in incoming)
        {
            var existing = await _db.Bookings.FirstOrDefaultAsync(b =>
                b.HotelId == hotelId &&
                b.OTAType == dto.OTAType &&
                b.OTABookingId == dto.ExternalBookingId, ct);

            if (existing == null)
            {
                _db.Bookings.Add(MapToBookingEntity(hotelId, dto));
                created++;
            }
            else
            {
                UpdateBookingEntity(existing, dto);
                updated++;
            }
        }

        await _db.SaveChangesAsync(ct);
        return (created, updated);
    }

    private static Booking MapToBookingEntity(int hotelId, StandardBookingDto dto) => new()
    {
        HotelId = hotelId,
        OTABookingId = dto.ExternalBookingId,
        PmsConfirmationNumber = dto.PmsConfirmationNumber,
        OTAType = dto.OTAType,
        Source = dto.Source,
        GuestName = dto.GuestName,
        GuestEmail = dto.GuestEmail,
        GuestPhone = dto.GuestPhone,
        GuestNationality = dto.GuestNationality,
        CheckInDate = dto.CheckInDate,
        CheckOutDate = dto.CheckOutDate,
        Adults = dto.Adults,
        Children = dto.Children,
        RoomType = dto.RoomType,
        RoomNumber = dto.RoomNumber,
        TotalPrice = dto.TotalPrice,
        Currency = dto.Currency,
        RatePlan = dto.RatePlan,
        BookingStatus = dto.Status,
        SpecialRequests = dto.SpecialRequests,
        BookingDate = dto.BookingDate,
        CreatedDate = DateTime.UtcNow,
        UpdatedDate = DateTime.UtcNow
    };

    private static void UpdateBookingEntity(Booking existing, StandardBookingDto dto)
    {
        existing.BookingStatus = dto.Status;
        existing.CheckInDate = dto.CheckInDate;
        existing.CheckOutDate = dto.CheckOutDate;
        existing.TotalPrice = dto.TotalPrice;
        existing.RoomType = dto.RoomType;
        existing.RoomNumber = dto.RoomNumber;
        existing.SpecialRequests = dto.SpecialRequests;
        existing.UpdatedDate = DateTime.UtcNow;
    }

    private async Task CreateNotificationAsync(int hotelId, StandardBookingDto booking, CancellationToken ct)
    {
        _db.Notifications.Add(new Notification
        {
            HotelId = hotelId,
            Type = NotificationType.NewBooking,
            Title = "New Booking",
            Message = $"New booking from {booking.OTAType}: {booking.GuestName} " +
                      $"({booking.CheckInDate:dd MMM} – {booking.CheckOutDate:dd MMM})",
            DataJson = System.Text.Json.JsonSerializer.Serialize(booking),
            CreatedDate = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
    }

    // ─── Sync log helpers ─────────────────────────────────────────────────────

    private async Task<SyncLog> StartSyncLogAsync(
        int hotelId, OTAType? otaType, PmsType? pmsType, CancellationToken ct)
    {
        var log = new SyncLog
        {
            HotelId = hotelId,
            OTAType = otaType,
            PmsType = pmsType,
            SyncStartTime = DateTime.UtcNow,
            Status = SyncStatus.Running
        };
        _db.SyncLogs.Add(log);
        await _db.SaveChangesAsync(ct);
        return log;
    }

    private async Task FinishSyncLogAsync(
        SyncLog log, SyncStatus status,
        int processed, int created, int updated, int failed,
        string? error, CancellationToken ct)
    {
        log.SyncEndTime = DateTime.UtcNow;
        log.Status = status;
        log.RecordsProcessed = processed;
        log.RecordsCreated = created;
        log.RecordsUpdated = updated;
        log.RecordsFailed = failed;
        log.ErrorMessage = error;
        await _db.SaveChangesAsync(ct);
    }
}
