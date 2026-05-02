using HotelBookingSaaS.API.Data;
using HotelBookingSaaS.API.DTOs;
using HotelBookingSaaS.API.Enums;
using HotelBookingSaaS.API.Services;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelBookingSaaS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BookingsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IBackgroundJobClient _jobs;
    private readonly ILogger<BookingsController> _logger;

    public BookingsController(
        ApplicationDbContext db,
        IBackgroundJobClient jobs,
        ILogger<BookingsController> logger)
    {
        _db = db;
        _jobs = jobs;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<BookingResponseDto>>>> GetAll(
        [FromQuery] BookingQueryDto query)
    {
        var hotelIdFilter = GetHotelIdFilter(query.HotelId);
        if (hotelIdFilter == null && !User.IsInRole("SuperAdmin"))
            return Forbid();

        var q = _db.Bookings
            .Include(b => b.Hotel)
            .AsQueryable();

        if (hotelIdFilter.HasValue)
            q = q.Where(b => b.HotelId == hotelIdFilter.Value);

        if (query.OTAType.HasValue)
            q = q.Where(b => b.OTAType == query.OTAType.Value);

        if (query.Status.HasValue)
            q = q.Where(b => b.BookingStatus == query.Status.Value);

        if (query.CheckInFrom.HasValue)
            q = q.Where(b => b.CheckInDate >= query.CheckInFrom.Value);

        if (query.CheckInTo.HasValue)
            q = q.Where(b => b.CheckInDate <= query.CheckInTo.Value);

        if (!string.IsNullOrWhiteSpace(query.GuestName))
            q = q.Where(b => b.GuestName.Contains(query.GuestName));

        // Sorting
        q = (query.SortBy?.ToLower(), query.SortDesc) switch
        {
            ("checkindate", false) => q.OrderBy(b => b.CheckInDate),
            ("checkindate", true) => q.OrderByDescending(b => b.CheckInDate),
            ("guestname", false) => q.OrderBy(b => b.GuestName),
            ("guestname", true) => q.OrderByDescending(b => b.GuestName),
            ("createdate", false) => q.OrderBy(b => b.CreatedDate),
            ("createdate", true) => q.OrderByDescending(b => b.CreatedDate),
            _ => q.OrderByDescending(b => b.CheckInDate)
        };

        var total = await q.CountAsync();
        var items = await q
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(b => ToDto(b))
            .ToListAsync();

        return Ok(ApiResponse<PagedResult<BookingResponseDto>>.Ok(new PagedResult<BookingResponseDto>
        {
            Items = items,
            TotalCount = total,
            Page = query.Page,
            PageSize = query.PageSize
        }));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<BookingResponseDto>>> GetById(int id)
    {
        var booking = await _db.Bookings
            .Include(b => b.Hotel)
            .FirstOrDefaultAsync(b => b.BookingId == id);

        if (booking == null)
            return NotFound(ApiResponse<BookingResponseDto>.Fail("Booking not found"));

        if (!CanAccessHotel(booking.HotelId))
            return Forbid();

        return Ok(ApiResponse<BookingResponseDto>.Ok(ToDto(booking)));
    }

    /// <summary>Trigger an immediate on-demand sync for a hotel.</summary>
    [HttpPost("sync/{hotelId:int}")]
    [Authorize(Roles = "SuperAdmin,HotelAdmin,Manager")]
    public ActionResult<ApiResponse<string>> TriggerSync(int hotelId)
    {
        if (!CanAccessHotel(hotelId))
            return Forbid();

        var jobId = _jobs.Enqueue<Jobs.SyncHangfireJob>(j => j.RunHotelSync(hotelId));
        _logger.LogInformation("On-demand sync enqueued for hotel {HotelId}, job {JobId}", hotelId, jobId);

        return Ok(ApiResponse<string>.Ok(jobId, "Sync enqueued. Job ID: " + jobId));
    }

    [HttpPatch("{id:int}/status")]
    [Authorize(Roles = "SuperAdmin,HotelAdmin,Manager")]
    public async Task<ActionResult<ApiResponse<bool>>> UpdateStatus(
        int id, [FromBody] BookingStatus status)
    {
        var booking = await _db.Bookings.FindAsync(id);
        if (booking == null)
            return NotFound(ApiResponse<bool>.Fail("Booking not found"));

        if (!CanAccessHotel(booking.HotelId))
            return Forbid();

        booking.BookingStatus = status;
        if (status == BookingStatus.Cancelled)
        {
            booking.CancelledDate = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync();

        return Ok(ApiResponse<bool>.Ok(true));
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private int? GetHotelIdFilter(int? requestedHotelId)
    {
        if (User.IsInRole("SuperAdmin"))
            return requestedHotelId; // null = all hotels

        var userHotelId = GetCurrentUserHotelId();
        // Hotel users can only query their own hotel
        if (requestedHotelId.HasValue && requestedHotelId != userHotelId)
            return -1; // Force empty result for unauthorized hotel
        return userHotelId;
    }

    private bool CanAccessHotel(int hotelId)
    {
        if (User.IsInRole("SuperAdmin")) return true;
        return GetCurrentUserHotelId() == hotelId;
    }

    private int? GetCurrentUserHotelId()
    {
        var claim = User.FindFirst("hotelId")?.Value;
        return int.TryParse(claim, out var id) ? id : null;
    }

    private static BookingResponseDto ToDto(Models.Booking b) => new()
    {
        BookingId = b.BookingId,
        HotelId = b.HotelId,
        HotelName = b.Hotel?.HotelName ?? string.Empty,
        OTABookingId = b.OTABookingId,
        OTAType = b.OTAType,
        OTATypeName = b.OTAType.ToString(),
        GuestName = b.GuestName,
        GuestEmail = b.GuestEmail,
        GuestPhone = b.GuestPhone,
        CheckInDate = b.CheckInDate,
        CheckOutDate = b.CheckOutDate,
        Nights = (int)(b.CheckOutDate - b.CheckInDate).TotalDays,
        RoomType = b.RoomType,
        TotalPrice = b.TotalPrice,
        Currency = b.Currency,
        Status = b.BookingStatus,
        StatusName = b.BookingStatus.ToString(),
        CreatedDate = b.CreatedDate
    };
}
