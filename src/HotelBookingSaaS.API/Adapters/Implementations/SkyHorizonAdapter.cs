using HotelBookingSaaS.API.Adapters.Base;
using HotelBookingSaaS.API.DTOs;
using HotelBookingSaaS.API.Enums;
using HotelBookingSaaS.API.Models;
using HotelBookingSaaS.API.Services;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text.Json;

namespace HotelBookingSaaS.API.Adapters.Implementations;

/// <summary>
/// Adapter for Sky Horizon PMS.
/// Sky Horizon uses Basic Auth + REST JSON API.
/// Base URL typically: https://api.skyhorizon.com/v2/
/// </summary>
public class SkyHorizonAdapter : BaseHotelPmsAdapter
{
    public override PmsType PmsType => PmsType.SkyHorizon;
    public override string PmsName => "Sky Horizon";

    public SkyHorizonAdapter(
        IHttpClientFactory httpClientFactory,
        IEncryptionService encryptionService,
        ILogger<SkyHorizonAdapter> logger)
        : base(httpClientFactory, encryptionService, logger) { }

    protected override void ConfigureHttpClient(HttpClient client, PMSCredential credential)
    {
        base.ConfigureHttpClient(client, credential);

        // Sky Horizon uses Basic Auth (username:password)
        var username = DecryptField(credential.UsernameEncrypted);
        var password = DecryptField(credential.PasswordEncrypted);

        if (!string.IsNullOrEmpty(username))
        {
            var encoded = Convert.ToBase64String(
                System.Text.Encoding.UTF8.GetBytes($"{username}:{password}"));
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", encoded);
        }
        else if (!string.IsNullOrEmpty(credential.ApiKeyEncrypted))
        {
            var apiKey = DecryptField(credential.ApiKeyEncrypted);
            client.DefaultRequestHeaders.Add("X-API-Key", apiKey);
        }
    }

    public override async Task<CredentialTestResultDto> TestConnectionAsync(CancellationToken ct = default)
    {
        EnsureInitialized();
        try
        {
            // Sky Horizon health/ping endpoint
            var result = await WithRetryAsync(async () =>
                await GetAsync<SkyHorizonPingResponse>("health/ping", ct), ct: ct);

            return new CredentialTestResultDto
            {
                IsConnected = result?.Status == "ok",
                Message = result?.Status == "ok"
                    ? $"Connected to Sky Horizon (v{result.Version})"
                    : "Connection failed: unexpected response",
                TestedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Sky Horizon connection test failed for hotel {HotelId}",
                Credential?.HotelId);
            return new CredentialTestResultDto
            {
                IsConnected = false,
                Message = $"Connection failed: {ex.Message}",
                TestedAt = DateTime.UtcNow
            };
        }
    }

    public override async Task<IEnumerable<StandardBookingDto>> GetBookingsAsync(
        DateTime fromDate, DateTime toDate, CancellationToken ct = default)
    {
        EnsureInitialized();

        var endpoint = $"reservations?arrival_from={fromDate:yyyy-MM-dd}&arrival_to={toDate:yyyy-MM-dd}&status=all";

        var response = await WithRetryAsync(async () =>
            await GetAsync<SkyHorizonReservationListResponse>(endpoint, ct), ct: ct);

        if (response?.Reservations == null) return Enumerable.Empty<StandardBookingDto>();

        return response.Reservations.Select(MapToStandard);
    }

    public override async Task<IEnumerable<StandardRoomAvailabilityDto>> GetAvailabilityAsync(
        DateTime fromDate, DateTime toDate,
        IEnumerable<string>? roomTypeIds = null,
        CancellationToken ct = default)
    {
        EnsureInitialized();

        var endpoint = $"availability?from={fromDate:yyyy-MM-dd}&to={toDate:yyyy-MM-dd}";
        var response = await WithRetryAsync(async () =>
            await GetAsync<SkyHorizonAvailabilityResponse>(endpoint, ct), ct: ct);

        if (response?.RoomTypes == null) return Enumerable.Empty<StandardRoomAvailabilityDto>();

        return response.RoomTypes
            .Where(rt => roomTypeIds == null || roomTypeIds.Contains(rt.Id))
            .SelectMany(rt => rt.Dates.Select(d => new StandardRoomAvailabilityDto
            {
                RoomTypeId = rt.Id,
                RoomTypeName = rt.Name,
                Date = DateTime.Parse(d.Date),
                AvailableCount = d.Available,
                Price = d.Rate,
                Currency = response.Currency ?? "JOD"
            }));
    }

    public override async Task<string?> CreateBookingAsync(
        StandardBookingDto booking, CancellationToken ct = default)
    {
        EnsureInitialized();

        var request = new SkyHorizonCreateReservationRequest
        {
            GuestName = booking.GuestName,
            GuestEmail = booking.GuestEmail,
            GuestPhone = booking.GuestPhone,
            ArrivalDate = booking.CheckInDate.ToString("yyyy-MM-dd"),
            DepartureDate = booking.CheckOutDate.ToString("yyyy-MM-dd"),
            Adults = booking.Adults,
            Children = booking.Children,
            RoomType = booking.RoomType,
            TotalAmount = booking.TotalPrice,
            Currency = booking.Currency,
            Source = booking.OTAType.ToString(),
            SpecialRequests = booking.SpecialRequests
        };

        var response = await WithRetryAsync(async () =>
            await PostAsync<SkyHorizonCreateResponse>("reservations", request, ct), ct: ct);

        if (response?.Success == true)
        {
            Logger.LogInformation("Sky Horizon: created reservation {Id} for hotel {HotelId}",
                response.ConfirmationNumber, Credential?.HotelId);
            return response.ConfirmationNumber;
        }

        Logger.LogWarning("Sky Horizon: failed to create reservation. Error: {Err}", response?.ErrorMessage);
        return null;
    }

    public override async Task<bool> UpdateBookingAsync(
        string pmsBookingId, StandardBookingDto booking, CancellationToken ct = default)
    {
        EnsureInitialized();

        var request = new
        {
            status = booking.Status.ToString().ToLower(),
            arrival_date = booking.CheckInDate.ToString("yyyy-MM-dd"),
            departure_date = booking.CheckOutDate.ToString("yyyy-MM-dd"),
            special_requests = booking.SpecialRequests
        };

        try
        {
            var response = await HttpClient!.PutAsync(
                $"reservations/{pmsBookingId}",
                JsonContent(request),
                ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Sky Horizon: failed to update reservation {Id}", pmsBookingId);
            return false;
        }
    }

    public override async Task<bool> CancelBookingAsync(
        string pmsBookingId, string? reason = null, CancellationToken ct = default)
    {
        EnsureInitialized();

        try
        {
            var request = new { reason = reason ?? "Cancelled via system" };
            var response = await HttpClient!.PostAsync(
                $"reservations/{pmsBookingId}/cancel",
                JsonContent(request),
                ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Sky Horizon: failed to cancel reservation {Id}", pmsBookingId);
            return false;
        }
    }

    // ─── Mapping ──────────────────────────────────────────────────────────────

    private static StandardBookingDto MapToStandard(SkyHorizonReservation r) => new()
    {
        ExternalBookingId = r.ReservationId,
        OTAType = OTAType.Direct,
        Source = BookingSource.PMS,
        GuestName = $"{r.GuestFirstName} {r.GuestLastName}".Trim(),
        GuestEmail = r.GuestEmail,
        GuestPhone = r.GuestPhone,
        GuestNationality = r.GuestNationality,
        CheckInDate = DateTime.Parse(r.ArrivalDate),
        CheckOutDate = DateTime.Parse(r.DepartureDate),
        Adults = r.Adults,
        Children = r.Children,
        RoomType = r.RoomType,
        RoomNumber = r.RoomNumber,
        TotalPrice = r.TotalAmount,
        Currency = r.Currency ?? "JOD",
        RatePlan = r.RatePlan,
        Status = MapStatus(r.Status),
        SpecialRequests = r.SpecialRequests,
        BookingDate = DateTime.TryParse(r.CreatedAt, out var d) ? d : DateTime.UtcNow,
        PmsConfirmationNumber = r.ConfirmationNumber,
        RawData = new Dictionary<string, string>
        {
            ["pms_id"] = r.ReservationId,
            ["pms_type"] = "SkyHorizon"
        }
    };

    // ─── Sky Horizon API response models (internal) ───────────────────────────

    private record SkyHorizonPingResponse(string Status, string Version);

    private record SkyHorizonReservationListResponse(
        List<SkyHorizonReservation> Reservations,
        int Total);

    private record SkyHorizonReservation(
        string ReservationId,
        string ConfirmationNumber,
        string GuestFirstName,
        string GuestLastName,
        string? GuestEmail,
        string? GuestPhone,
        string? GuestNationality,
        string ArrivalDate,
        string DepartureDate,
        int Adults,
        int Children,
        string? RoomType,
        string? RoomNumber,
        decimal TotalAmount,
        string? Currency,
        string? RatePlan,
        string Status,
        string? SpecialRequests,
        string? CreatedAt);

    private record SkyHorizonAvailabilityResponse(
        List<SkyHorizonRoomTypeAvailability> RoomTypes,
        string? Currency);

    private record SkyHorizonRoomTypeAvailability(
        string Id,
        string Name,
        List<SkyHorizonDateAvailability> Dates);

    private record SkyHorizonDateAvailability(
        string Date,
        int Available,
        decimal Rate);

    private record SkyHorizonCreateReservationRequest
    {
        public string GuestName { get; init; } = string.Empty;
        public string? GuestEmail { get; init; }
        public string? GuestPhone { get; init; }
        public string ArrivalDate { get; init; } = string.Empty;
        public string DepartureDate { get; init; } = string.Empty;
        public int Adults { get; init; }
        public int Children { get; init; }
        public string? RoomType { get; init; }
        public decimal TotalAmount { get; init; }
        public string Currency { get; init; } = "JOD";
        public string? Source { get; init; }
        public string? SpecialRequests { get; init; }
    }

    private record SkyHorizonCreateResponse(
        bool Success,
        string? ConfirmationNumber,
        string? ErrorMessage);
}
