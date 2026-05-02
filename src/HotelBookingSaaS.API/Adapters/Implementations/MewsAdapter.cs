using HotelBookingSaaS.API.Adapters.Base;
using HotelBookingSaaS.API.DTOs;
using HotelBookingSaaS.API.Enums;
using HotelBookingSaaS.API.Models;
using HotelBookingSaaS.API.Services;
using Microsoft.Extensions.Logging;

namespace HotelBookingSaaS.API.Adapters.Implementations;

/// <summary>
/// Adapter for Mews PMS (Mews Connector API v1).
/// Mews uses AccessToken (no OAuth) in the request body, not headers.
/// Base URL: https://api.mews.com/ (or https://api.mews-demo.com/ for sandbox)
/// Docs: https://mews-systems.gitbook.io/connector-api/
/// </summary>
public class MewsAdapter : BaseHotelPmsAdapter
{
    public override PmsType PmsType => PmsType.Mews;
    public override string PmsName => "Mews";

    private string _accessToken = string.Empty;
    private string _clientToken = string.Empty;

    public MewsAdapter(
        IHttpClientFactory httpClientFactory,
        IEncryptionService encryptionService,
        ILogger<MewsAdapter> logger)
        : base(httpClientFactory, encryptionService, logger) { }

    public override void Initialize(PMSCredential credential)
    {
        Credential = credential;
        HttpClient = HttpClientFactory.CreateClient(PmsName);

        if (!string.IsNullOrEmpty(credential.ApiUrl))
            HttpClient.BaseAddress = new Uri(credential.ApiUrl.TrimEnd('/') + "/");

        HttpClient.Timeout = TimeSpan.FromSeconds(30);
        HttpClient.DefaultRequestHeaders.Accept.Add(
            new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

        // Mews tokens are passed in the body, not headers
        _accessToken = DecryptField(credential.AccessTokenEncrypted);
        _clientToken = DecryptField(credential.ClientIdEncrypted);

        Logger.LogInformation("Mews adapter initialised for hotel {HotelId}", credential.HotelId);
    }

    public override async Task<CredentialTestResultDto> TestConnectionAsync(CancellationToken ct = default)
    {
        EnsureInitialized();
        try
        {
            var result = await WithRetryAsync(async () =>
                await PostAsync<MewsConfigurationResponse>(
                    "api/connector/v1/configuration/get",
                    new MewsRequest(_clientToken, _accessToken),
                    ct), ct: ct);

            return new CredentialTestResultDto
            {
                IsConnected = result?.Enterprise != null,
                Message = result?.Enterprise != null
                    ? $"Connected to Mews. Property: {result.Enterprise.Name}"
                    : "Connection failed",
                TestedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Mews connection test failed for hotel {HotelId}", Credential?.HotelId);
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

        // Mews uses cursor-based pagination; loop until exhausted
        var bookings = new List<StandardBookingDto>();
        string? cursor = null;

        do
        {
            var request = new MewsReservationRequest(
                _clientToken, _accessToken,
                new MewsTimeFilter("StartUtc", fromDate, toDate),
                Limitation: new MewsLimitation(100, cursor));

            var response = await WithRetryAsync(async () =>
                await PostAsync<MewsReservationResponse>(
                    "api/connector/v1/reservations/getAll",
                    request, ct), ct: ct);

            if (response?.Reservations == null) break;

            bookings.AddRange(response.Reservations.Select(MapToStandard));
            cursor = response.Cursor;

        } while (cursor != null);

        return bookings;
    }

    public override async Task<IEnumerable<StandardRoomAvailabilityDto>> GetAvailabilityAsync(
        DateTime fromDate, DateTime toDate,
        IEnumerable<string>? roomTypeIds = null,
        CancellationToken ct = default)
    {
        EnsureInitialized();

        var request = new
        {
            ClientToken = _clientToken,
            AccessToken = _accessToken,
            StartUtc = fromDate,
            EndUtc = toDate
        };

        var response = await WithRetryAsync(async () =>
            await PostAsync<MewsAvailabilityResponse>(
                "api/connector/v1/services/getAvailability", request, ct), ct: ct);

        if (response?.CategoryAvailabilities == null)
            return Enumerable.Empty<StandardRoomAvailabilityDto>();

        return response.CategoryAvailabilities
            .Where(ca => roomTypeIds == null || roomTypeIds.Contains(ca.SpaceCategoryId))
            .SelectMany(ca => ca.Availabilities.Select(a => new StandardRoomAvailabilityDto
            {
                RoomTypeId = ca.SpaceCategoryId,
                RoomTypeName = ca.SpaceCategoryName ?? ca.SpaceCategoryId,
                Date = a.Date,
                AvailableCount = a.Available,
                Price = a.AverageRate,
                Currency = response.CurrencyCode ?? "JOD"
            }));
    }

    public override async Task<string?> CreateBookingAsync(
        StandardBookingDto booking, CancellationToken ct = default)
    {
        EnsureInitialized();

        var request = new
        {
            ClientToken = _clientToken,
            AccessToken = _accessToken,
            Reservations = new[]
            {
                new
                {
                    Identifier = Guid.NewGuid().ToString(),
                    StartUtc = booking.CheckInDate,
                    EndUtc = booking.CheckOutDate,
                    AdultCount = booking.Adults,
                    ChildCount = booking.Children,
                    Notes = booking.SpecialRequests,
                    Customer = new
                    {
                        Email = booking.GuestEmail,
                        FirstName = booking.GuestName.Split(' ').First(),
                        LastName = booking.GuestName.Split(' ').LastOrDefault() ?? string.Empty,
                        Phone = booking.GuestPhone
                    }
                }
            }
        };

        var response = await WithRetryAsync(async () =>
            await PostAsync<MewsCreateResponse>(
                "api/connector/v1/reservations/add", request, ct), ct: ct);

        var confirmationNumber = response?.Reservations?.FirstOrDefault()?.Number;
        if (confirmationNumber != null)
            Logger.LogInformation("Mews: created reservation {Num} for hotel {HotelId}",
                confirmationNumber, Credential?.HotelId);

        return confirmationNumber;
    }

    public override async Task<bool> UpdateBookingAsync(
        string pmsBookingId, StandardBookingDto booking, CancellationToken ct = default)
    {
        EnsureInitialized();
        try
        {
            var request = new
            {
                ClientToken = _clientToken,
                AccessToken = _accessToken,
                ReservationId = pmsBookingId,
                StartUtc = booking.CheckInDate,
                EndUtc = booking.CheckOutDate
            };
            await PostAsync<object>("api/connector/v1/reservations/updateInterval", request, ct);
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Mews: failed to update reservation {Id}", pmsBookingId);
            return false;
        }
    }

    public override async Task<bool> CancelBookingAsync(
        string pmsBookingId, string? reason = null, CancellationToken ct = default)
    {
        EnsureInitialized();
        try
        {
            var request = new
            {
                ClientToken = _clientToken,
                AccessToken = _accessToken,
                ReservationId = pmsBookingId,
                Notes = reason ?? "Cancelled via integration"
            };
            await PostAsync<object>("api/connector/v1/reservations/cancel", request, ct);
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Mews: failed to cancel reservation {Id}", pmsBookingId);
            return false;
        }
    }

    // ─── Mapping ──────────────────────────────────────────────────────────────

    private static StandardBookingDto MapToStandard(MewsReservation r) => new()
    {
        ExternalBookingId = r.Id,
        OTAType = OTAType.Direct,
        Source = BookingSource.PMS,
        GuestName = $"{r.Customer?.FirstName} {r.Customer?.LastName}".Trim(),
        GuestEmail = r.Customer?.Email,
        GuestPhone = r.Customer?.Phone,
        CheckInDate = r.StartUtc,
        CheckOutDate = r.EndUtc,
        Adults = r.AdultCount,
        Children = r.ChildCount ?? 0,
        RoomType = r.AssignedSpaceCategory,
        TotalPrice = r.TotalAmount?.Net ?? 0,
        Currency = r.TotalAmount?.Currency ?? "JOD",
        Status = MapStatus(r.State),
        SpecialRequests = r.Notes,
        BookingDate = r.CreatedUtc,
        PmsConfirmationNumber = r.Number,
        RawData = new Dictionary<string, string>
        {
            ["pms_id"] = r.Id,
            ["pms_type"] = "Mews"
        }
    };

    // ─── Mews request/response models ─────────────────────────────────────────

    private record MewsRequest(string ClientToken, string AccessToken);

    private record MewsReservationRequest(
        string ClientToken,
        string AccessToken,
        MewsTimeFilter TimeFilter,
        MewsLimitation? Limitation = null);

    private record MewsTimeFilter(string Type, DateTime StartUtc, DateTime EndUtc);
    private record MewsLimitation(int Count, string? Cursor);

    private record MewsConfigurationResponse(MewsEnterprise? Enterprise);
    private record MewsEnterprise(string Id, string Name);

    private record MewsReservationResponse(
        List<MewsReservation> Reservations, string? Cursor);

    private record MewsReservation(
        string Id,
        string Number,
        DateTime StartUtc,
        DateTime EndUtc,
        int AdultCount,
        int? ChildCount,
        string State,
        string? Notes,
        DateTime CreatedUtc,
        string? AssignedSpaceCategory,
        MewsCustomer? Customer,
        MewsAmount? TotalAmount);

    private record MewsCustomer(
        string? FirstName, string? LastName, string? Email, string? Phone);

    private record MewsAmount(decimal Net, string Currency);

    private record MewsAvailabilityResponse(
        List<MewsCategoryAvailability> CategoryAvailabilities,
        string? CurrencyCode);

    private record MewsCategoryAvailability(
        string SpaceCategoryId,
        string? SpaceCategoryName,
        List<MewsDayAvailability> Availabilities);

    private record MewsDayAvailability(
        DateTime Date, int Available, decimal AverageRate);

    private record MewsCreateResponse(List<MewsCreatedReservation>? Reservations);
    private record MewsCreatedReservation(string Id, string Number);
}
