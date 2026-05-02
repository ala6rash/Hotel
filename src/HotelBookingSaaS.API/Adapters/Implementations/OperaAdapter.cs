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
/// Adapter for Oracle Opera PMS (Opera Cloud REST API v21.5+).
/// Uses OAuth 2.0 client_credentials flow.
/// Docs: https://docs.oracle.com/en/industries/hospitality/opera-cloud/
/// </summary>
public class OperaAdapter : BaseHotelPmsAdapter
{
    public override PmsType PmsType => PmsType.OracleOpera;
    public override string PmsName => "Oracle Opera";

    // Opera Cloud requires a hotel code in most requests
    private string _hotelCode = string.Empty;
    private string _accessToken = string.Empty;
    private DateTime _tokenExpiry = DateTime.MinValue;

    public OperaAdapter(
        IHttpClientFactory httpClientFactory,
        IEncryptionService encryptionService,
        ILogger<OperaAdapter> logger)
        : base(httpClientFactory, encryptionService, logger) { }

    protected override void ConfigureHttpClient(HttpClient client, PMSCredential credential)
    {
        base.ConfigureHttpClient(client, credential);

        // Opera Cloud additional headers
        client.DefaultRequestHeaders.Add("x-hotelid", GetHotelCode(credential));
        client.DefaultRequestHeaders.Add("x-app-key", DecryptField(credential.ClientIdEncrypted));
    }

    public override void Initialize(PMSCredential credential)
    {
        base.Initialize(credential);
        _hotelCode = GetHotelCode(credential);

        // Restore cached token if still valid
        if (!string.IsNullOrEmpty(credential.AccessTokenEncrypted) &&
            credential.TokenExpiresAt.HasValue &&
            credential.TokenExpiresAt.Value > DateTime.UtcNow.AddMinutes(5))
        {
            _accessToken = DecryptField(credential.AccessTokenEncrypted);
            _tokenExpiry = credential.TokenExpiresAt.Value;
            HttpClient!.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _accessToken);
        }
    }

    public override async Task<CredentialTestResultDto> TestConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            await EnsureTokenAsync(ct);
            var result = await WithRetryAsync(async () =>
                await GetAsync<OperaHotelInfoResponse>($"hsk/v1/hotels/{_hotelCode}", ct), ct: ct);

            return new CredentialTestResultDto
            {
                IsConnected = result?.Hotel != null,
                Message = result?.Hotel != null
                    ? $"Connected to Opera Cloud. Hotel: {result.Hotel.HotelName}"
                    : "Connection failed: hotel not found",
                TestedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Opera connection test failed for hotel {HotelId}", Credential?.HotelId);
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
        await EnsureTokenAsync(ct);

        // Opera Cloud pagination — fetch all pages
        var bookings = new List<StandardBookingDto>();
        int offset = 0;
        const int limit = 100;

        while (true)
        {
            var endpoint = $"rsv/v1/reservations?hotelId={_hotelCode}" +
                           $"&arrivalStartDate={fromDate:yyyy-MM-dd}" +
                           $"&arrivalEndDate={toDate:yyyy-MM-dd}" +
                           $"&limit={limit}&offset={offset}";

            var response = await WithRetryAsync(async () =>
                await GetAsync<OperaReservationListResponse>(endpoint, ct), ct: ct);

            if (response?.Reservations == null || !response.Reservations.Any()) break;

            bookings.AddRange(response.Reservations.Select(MapToStandard));

            if (response.Reservations.Count < limit) break;
            offset += limit;
        }

        return bookings;
    }

    public override async Task<IEnumerable<StandardRoomAvailabilityDto>> GetAvailabilityAsync(
        DateTime fromDate, DateTime toDate,
        IEnumerable<string>? roomTypeIds = null,
        CancellationToken ct = default)
    {
        EnsureInitialized();
        await EnsureTokenAsync(ct);

        var endpoint = $"par/v1/availability?hotelId={_hotelCode}" +
                       $"&startDate={fromDate:yyyy-MM-dd}&endDate={toDate:yyyy-MM-dd}";

        var response = await WithRetryAsync(async () =>
            await GetAsync<OperaAvailabilityResponse>(endpoint, ct), ct: ct);

        if (response?.RoomTypes == null) return Enumerable.Empty<StandardRoomAvailabilityDto>();

        return response.RoomTypes
            .Where(rt => roomTypeIds == null || roomTypeIds.Contains(rt.RoomTypeCode))
            .SelectMany(rt => rt.Availability.Select(a => new StandardRoomAvailabilityDto
            {
                RoomTypeId = rt.RoomTypeCode,
                RoomTypeName = rt.RoomTypeDescription,
                Date = DateTime.Parse(a.Date),
                AvailableCount = a.AvailableRooms,
                Price = a.Rate,
                Currency = a.CurrencyCode ?? "JOD"
            }));
    }

    public override async Task<string?> CreateBookingAsync(StandardBookingDto booking, CancellationToken ct = default)
    {
        EnsureInitialized();
        await EnsureTokenAsync(ct);

        var request = new
        {
            hotelId = _hotelCode,
            reservationGuests = new[]
            {
                new
                {
                    profileInfo = new
                    {
                        profile = new
                        {
                            customer = new
                            {
                                personName = new[]
                                {
                                    new { nameType = "PRIMARY", givenName = booking.GuestName.Split(' ').First(),
                                          surname = booking.GuestName.Split(' ').LastOrDefault() ?? string.Empty }
                                },
                                emailList = string.IsNullOrEmpty(booking.GuestEmail) ? null : new[]
                                {
                                    new { emailAddress = booking.GuestEmail }
                                }
                            }
                        }
                    }
                }
            },
            reservationInfo = new
            {
                arrival = booking.CheckInDate.ToString("yyyy-MM-dd"),
                departure = booking.CheckOutDate.ToString("yyyy-MM-dd"),
                adults = booking.Adults,
                children = booking.Children,
                roomType = booking.RoomType,
                ratePlanCode = booking.RatePlan ?? "BAR",
                reservationSource = "OTA"
            }
        };

        var response = await WithRetryAsync(async () =>
            await PostAsync<OperaCreateResponse>($"rsv/v1/hotels/{_hotelCode}/reservations", request, ct), ct: ct);

        return response?.ReservationIdList?.FirstOrDefault()?.Id;
    }

    public override async Task<bool> UpdateBookingAsync(
        string pmsBookingId, StandardBookingDto booking, CancellationToken ct = default)
    {
        EnsureInitialized();
        await EnsureTokenAsync(ct);

        try
        {
            var request = new
            {
                reservationStatus = booking.Status.ToString().ToUpper(),
                arrival = booking.CheckInDate.ToString("yyyy-MM-dd"),
                departure = booking.CheckOutDate.ToString("yyyy-MM-dd")
            };

            var content = JsonContent(request);
            var response = await HttpClient!.PatchAsync(
                $"rsv/v1/hotels/{_hotelCode}/reservations/{pmsBookingId}",
                content, ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Opera: failed to update reservation {Id}", pmsBookingId);
            return false;
        }
    }

    public override async Task<bool> CancelBookingAsync(
        string pmsBookingId, string? reason = null, CancellationToken ct = default)
    {
        EnsureInitialized();
        await EnsureTokenAsync(ct);

        try
        {
            var request = new
            {
                cancellations = new[]
                {
                    new { remarks = reason ?? "Cancelled via integration", cancellationCode = "CXL" }
                }
            };

            var content = JsonContent(request);
            var response = await HttpClient!.PutAsync(
                $"rsv/v1/hotels/{_hotelCode}/reservations/{pmsBookingId}/cancellations",
                content, ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Opera: failed to cancel reservation {Id}", pmsBookingId);
            return false;
        }
    }

    public override async Task<PMSCredential?> RefreshTokenIfNeededAsync(
        PMSCredential credential, CancellationToken ct = default)
    {
        if (_tokenExpiry > DateTime.UtcNow.AddMinutes(10))
            return null;

        await FetchNewTokenAsync(credential, ct);

        credential.AccessTokenEncrypted = EncryptionService.Encrypt(_accessToken);
        credential.TokenExpiresAt = _tokenExpiry;
        return credential;
    }

    // ─── OAuth token management ───────────────────────────────────────────────

    private async Task EnsureTokenAsync(CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(_accessToken) && _tokenExpiry > DateTime.UtcNow.AddMinutes(5))
        {
            HttpClient!.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _accessToken);
            return;
        }

        await FetchNewTokenAsync(Credential!, ct);
    }

    private async Task FetchNewTokenAsync(PMSCredential credential, CancellationToken ct)
    {
        // Opera Cloud uses standard OAuth2 client_credentials
        var tokenEndpoint = $"{credential.ApiUrl?.TrimEnd('/')}/oauth/token";
        var clientId = DecryptField(credential.ClientIdEncrypted);
        var clientSecret = DecryptField(credential.ClientSecretEncrypted);

        var body = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "client_credentials"),
            new KeyValuePair<string, string>("client_id", clientId),
            new KeyValuePair<string, string>("client_secret", clientSecret)
        });

        using var tokenClient = HttpClientFactory.CreateClient();
        var response = await tokenClient.PostAsync(tokenEndpoint, body, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        var tokenData = JsonSerializer.Deserialize<OperaTokenResponse>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        _accessToken = tokenData?.AccessToken ?? throw new Exception("Opera token response missing access_token");
        _tokenExpiry = DateTime.UtcNow.AddSeconds(tokenData.ExpiresIn - 60);

        HttpClient!.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _accessToken);
    }

    private static string GetHotelCode(PMSCredential credential)
    {
        // The Opera hotel code is stored in AdditionalConfigJson: {"hotelCode":"XXXXX"}
        if (string.IsNullOrEmpty(credential.AdditionalConfigJson)) return string.Empty;
        try
        {
            var doc = JsonDocument.Parse(credential.AdditionalConfigJson);
            return doc.RootElement.TryGetProperty("hotelCode", out var prop)
                ? prop.GetString() ?? string.Empty
                : string.Empty;
        }
        catch { return string.Empty; }
    }

    // ─── Mapping ──────────────────────────────────────────────────────────────

    private static StandardBookingDto MapToStandard(OperaReservation r) => new()
    {
        ExternalBookingId = r.ReservationId,
        OTAType = OTAType.Direct,
        Source = BookingSource.PMS,
        GuestName = r.GuestName,
        GuestEmail = r.GuestEmail,
        GuestPhone = r.GuestPhone,
        CheckInDate = DateTime.Parse(r.Arrival),
        CheckOutDate = DateTime.Parse(r.Departure),
        Adults = r.Adults,
        Children = r.Children,
        RoomType = r.RoomType,
        RoomNumber = r.RoomNumber,
        TotalPrice = r.TotalAmount,
        Currency = r.CurrencyCode ?? "JOD",
        RatePlan = r.RatePlanCode,
        Status = MapStatus(r.ReservationStatus),
        SpecialRequests = r.SpecialRequests,
        BookingDate = DateTime.TryParse(r.CreateDateTime, out var d) ? d : DateTime.UtcNow,
        PmsConfirmationNumber = r.ConfirmationNumber,
        RawData = new Dictionary<string, string>
        {
            ["pms_id"] = r.ReservationId,
            ["pms_type"] = "OracleOpera"
        }
    };

    // ─── Opera response models ─────────────────────────────────────────────────

    private record OperaTokenResponse(string AccessToken, int ExpiresIn, string TokenType);
    private record OperaHotelInfoResponse(OperaHotelInfo Hotel);
    private record OperaHotelInfo(string HotelCode, string HotelName);

    private record OperaReservationListResponse(
        List<OperaReservation> Reservations, int TotalPages, int TotalResults);

    private record OperaReservation(
        string ReservationId,
        string ConfirmationNumber,
        string GuestName,
        string? GuestEmail,
        string? GuestPhone,
        string Arrival,
        string Departure,
        int Adults,
        int Children,
        string? RoomType,
        string? RoomNumber,
        decimal TotalAmount,
        string? CurrencyCode,
        string? RatePlanCode,
        string ReservationStatus,
        string? SpecialRequests,
        string? CreateDateTime);

    private record OperaAvailabilityResponse(List<OperaRoomTypeAvailability> RoomTypes);

    private record OperaRoomTypeAvailability(
        string RoomTypeCode,
        string RoomTypeDescription,
        List<OperaDateAvailability> Availability);

    private record OperaDateAvailability(
        string Date, int AvailableRooms, decimal Rate, string? CurrencyCode);

    private record OperaCreateResponse(
        List<OperaReservationId> ReservationIdList);

    private record OperaReservationId(string Id, string Type);
}
