using HotelBookingSaaS.API.Adapters.Interfaces;
using HotelBookingSaaS.API.DTOs;
using HotelBookingSaaS.API.Enums;
using HotelBookingSaaS.API.Models;
using HotelBookingSaaS.API.Services;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace HotelBookingSaaS.API.Adapters.Implementations;

/// <summary>
/// Channex Channel Manager adapter — the primary OTA gateway.
/// Channex bridges to 50+ OTAs (Booking.com, Airbnb, Expedia, Agoda, etc).
/// Uses API Key auth: header "user-api-key: <key>".
/// Docs: https://channex.io/api-docs/
/// </summary>
public class ChannexAdapter : IOTAAdapter
{
    public OTAType OTAType => OTAType.Channex;
    public string OTAName => "Channex";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IEncryptionService _encryptionService;
    private readonly ILogger<ChannexAdapter> _logger;

    private OTACredential? _credential;
    private HttpClient? _client;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ChannexAdapter(
        IHttpClientFactory httpClientFactory,
        IEncryptionService encryptionService,
        ILogger<ChannexAdapter> logger)
    {
        _httpClientFactory = httpClientFactory;
        _encryptionService = encryptionService;
        _logger = logger;
    }

    public void Initialize(OTACredential credential)
    {
        _credential = credential ?? throw new ArgumentNullException(nameof(credential));
        _client = _httpClientFactory.CreateClient("Channex");
        _client.BaseAddress = new Uri("https://api.channex.io/api/v1/");
        _client.Timeout = TimeSpan.FromSeconds(30);
        _client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));

        var apiKey = _encryptionService.Decrypt(credential.ApiKeyEncrypted ?? string.Empty);
        _client.DefaultRequestHeaders.Add("user-api-key", apiKey);
    }

    public async Task<CredentialTestResultDto> TestConnectionAsync(CancellationToken ct = default)
    {
        EnsureInitialized();
        try
        {
            var response = await _client!.GetAsync("properties", ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<ChannexPropertiesResponse>(json, JsonOpts);
            var count = result?.Data?.Count ?? 0;

            return new CredentialTestResultDto
            {
                IsConnected = true,
                Message = $"Channex connected. {count} property(ies) found.",
                TestedAt = DateTime.UtcNow,
                BookingsFound = count
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Channex connection test failed for hotel {HotelId}", _credential?.HotelId);
            return new CredentialTestResultDto
            {
                IsConnected = false,
                Message = $"Channex connection failed: {ex.Message}",
                TestedAt = DateTime.UtcNow
            };
        }
    }

    public async Task<IEnumerable<StandardBookingDto>> GetBookingsAsync(
        DateTime fromDate, DateTime toDate, CancellationToken ct = default)
    {
        EnsureInitialized();

        var bookings = new List<StandardBookingDto>();
        int page = 1;
        const int perPage = 100;

        while (true)
        {
            var propertyFilter = !string.IsNullOrEmpty(_credential!.PropertyId)
                ? $"&filter[property_id]={_credential.PropertyId}"
                : string.Empty;

            var url = $"bookings?filter[date_from]={fromDate:yyyy-MM-dd}" +
                      $"&filter[date_to]={toDate:yyyy-MM-dd}" +
                      $"&pagination[page]={page}&pagination[per_page]={perPage}" +
                      propertyFilter;

            var response = await WithRetryAsync(async () =>
            {
                var r = await _client!.GetAsync(url, ct);
                r.EnsureSuccessStatusCode();
                return r;
            }, ct: ct);

            var json = await response.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<ChannexBookingListResponse>(json, JsonOpts);

            if (result?.Data == null || !result.Data.Any()) break;

            bookings.AddRange(result.Data.Select(MapToStandard));

            if (result.Meta?.TotalPages <= page) break;
            page++;
        }

        return bookings;
    }

    public async Task<bool> UpdateAvailabilityAsync(
        IEnumerable<StandardRoomAvailabilityDto> availability,
        CancellationToken ct = default)
    {
        EnsureInitialized();

        // Channex ARI (availability/rate/inventory) update
        var grouped = availability.GroupBy(a => a.RoomTypeId);
        bool allSuccess = true;

        foreach (var group in grouped)
        {
            var payload = new
            {
                values = group.Select(a => new
                {
                    property_id = _credential!.PropertyId,
                    room_type_id = group.Key,
                    date = a.Date.ToString("yyyy-MM-dd"),
                    availability = a.AvailableCount,
                    rate = a.Price,
                    currency = a.Currency,
                    stop_sell = a.AvailableCount == 0
                }).ToArray()
            };

            try
            {
                var content = new StringContent(
                    JsonSerializer.Serialize(payload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
                    Encoding.UTF8, "application/json");

                var response = await _client!.PostAsync("availability", content, ct);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Channex: availability update failed for room type {RoomType}", group.Key);
                    allSuccess = false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Channex: exception updating availability for {RoomType}", group.Key);
                allSuccess = false;
            }
        }

        return allSuccess;
    }

    /// <summary>
    /// Fetches the Channex channel ID for a specific OTA (e.g. Booking.com).
    /// Used during onboarding to wire up channels.
    /// </summary>
    public async Task<string?> GetChannelIdForOTAAsync(OTAType otaType, CancellationToken ct = default)
    {
        EnsureInitialized();
        var otaName = otaType switch
        {
            OTAType.BookingCom => "Booking.com",
            OTAType.Airbnb => "Airbnb",
            OTAType.Expedia => "Expedia",
            OTAType.Agoda => "Agoda",
            _ => otaType.ToString()
        };

        var response = await _client!.GetAsync($"channels?filter[title]={Uri.EscapeDataString(otaName)}", ct);
        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadAsStringAsync(ct);
        var result = JsonSerializer.Deserialize<ChannexChannelListResponse>(json, JsonOpts);
        return result?.Data?.FirstOrDefault()?.Id;
    }

    // ─── Retry helper ─────────────────────────────────────────────────────────

    private async Task<T> WithRetryAsync<T>(Func<Task<T>> op, int maxAttempts = 3, CancellationToken ct = default)
    {
        var delay = TimeSpan.FromSeconds(2);
        Exception? lastEx = null;
        for (int i = 1; i <= maxAttempts; i++)
        {
            try { return await op(); }
            catch (Exception ex) when (i < maxAttempts)
            {
                lastEx = ex;
                await Task.Delay(delay, ct);
                delay *= 2;
            }
        }
        throw lastEx!;
    }

    private void EnsureInitialized()
    {
        if (_client == null)
            throw new InvalidOperationException("ChannexAdapter not initialised. Call Initialize() first.");
    }

    // ─── Mapping ──────────────────────────────────────────────────────────────

    private static StandardBookingDto MapToStandard(ChannexBooking b) => new()
    {
        ExternalBookingId = b.Id,
        OTAType = MapOTAType(b.ChannelTitle),
        Source = BookingSource.OTA,
        GuestName = $"{b.GuestFirstName} {b.GuestLastName}".Trim(),
        GuestEmail = b.GuestEmail,
        GuestPhone = b.GuestPhone,
        CheckInDate = b.ArrivalDate,
        CheckOutDate = b.DepartureDate,
        Adults = b.Adults ?? 1,
        Children = b.Children ?? 0,
        RoomType = b.RoomTypeName,
        TotalPrice = b.TotalPrice ?? 0,
        Currency = b.Currency ?? "JOD",
        Status = MapChannexStatus(b.Status),
        SpecialRequests = b.Notes,
        BookingDate = b.InsertedAt,
        RawData = new Dictionary<string, string>
        {
            ["channex_id"] = b.Id,
            ["channel"] = b.ChannelTitle ?? string.Empty
        }
    };

    private static OTAType MapOTAType(string? channelTitle) => channelTitle?.ToLower() switch
    {
        "booking.com" => OTAType.BookingCom,
        "airbnb" => OTAType.Airbnb,
        "expedia" => OTAType.Expedia,
        "agoda" => OTAType.Agoda,
        "hotels.com" => OTAType.Hotels,
        _ => OTAType.Channex
    };

    private static BookingStatus MapChannexStatus(string? status) => status?.ToLower() switch
    {
        "confirmed" => BookingStatus.Confirmed,
        "modified" => BookingStatus.Confirmed,
        "cancelled" or "canceled" => BookingStatus.Cancelled,
        "new" => BookingStatus.Pending,
        _ => BookingStatus.Pending
    };

    // ─── Channex response models ───────────────────────────────────────────────

    private record ChannexPropertiesResponse(List<ChannexProperty>? Data, ChannexMeta? Meta);
    private record ChannexProperty(string Id, string Title);

    private record ChannexBookingListResponse(List<ChannexBooking>? Data, ChannexMeta? Meta);

    private record ChannexBooking(
        string Id,
        string? ChannelTitle,
        string GuestFirstName,
        string GuestLastName,
        string? GuestEmail,
        string? GuestPhone,
        DateTime ArrivalDate,
        DateTime DepartureDate,
        int? Adults,
        int? Children,
        string? RoomTypeName,
        decimal? TotalPrice,
        string? Currency,
        string? Status,
        string? Notes,
        DateTime InsertedAt);

    private record ChannexMeta(int CurrentPage, int TotalPages, int TotalCount);

    private record ChannexChannelListResponse(List<ChannexChannel>? Data);
    private record ChannexChannel(string Id, string Title);
}
