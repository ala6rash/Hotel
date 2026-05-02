using HotelBookingSaaS.API.Adapters.Interfaces;
using HotelBookingSaaS.API.DTOs;
using HotelBookingSaaS.API.Enums;
using HotelBookingSaaS.API.Models;
using HotelBookingSaaS.API.Services;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace HotelBookingSaaS.API.Adapters.Base;

/// <summary>
/// Shared plumbing for all PMS adapters: HTTP client management, credential
/// decryption, retry logic, and response parsing helpers.
/// Concrete adapters only implement the PMS-specific API calls.
/// </summary>
public abstract class BaseHotelPmsAdapter : IHotelPmsAdapter
{
    protected readonly IHttpClientFactory HttpClientFactory;
    protected readonly IEncryptionService EncryptionService;
    protected readonly ILogger Logger;

    protected PMSCredential? Credential;
    protected HttpClient? HttpClient;

    // Derived classes expose these
    public abstract PmsType PmsType { get; }
    public abstract string PmsName { get; }

    protected BaseHotelPmsAdapter(
        IHttpClientFactory httpClientFactory,
        IEncryptionService encryptionService,
        ILogger logger)
    {
        HttpClientFactory = httpClientFactory;
        EncryptionService = encryptionService;
        Logger = logger;
    }

    // ─── IHotelPmsAdapter ─────────────────────────────────────────────────────

    public virtual void Initialize(PMSCredential credential)
    {
        Credential = credential ?? throw new ArgumentNullException(nameof(credential));
        HttpClient = HttpClientFactory.CreateClient(PmsName);
        ConfigureHttpClient(HttpClient, credential);
        Logger.LogInformation("Adapter {Pms} initialised for hotel {HotelId}", PmsName, credential.HotelId);
    }

    public abstract Task<CredentialTestResultDto> TestConnectionAsync(CancellationToken ct = default);

    public abstract Task<IEnumerable<StandardBookingDto>> GetBookingsAsync(
        DateTime fromDate, DateTime toDate, CancellationToken ct = default);

    public abstract Task<IEnumerable<StandardRoomAvailabilityDto>> GetAvailabilityAsync(
        DateTime fromDate, DateTime toDate, IEnumerable<string>? roomTypeIds = null, CancellationToken ct = default);

    public abstract Task<string?> CreateBookingAsync(StandardBookingDto booking, CancellationToken ct = default);

    public abstract Task<bool> UpdateBookingAsync(string pmsBookingId, StandardBookingDto booking, CancellationToken ct = default);

    public abstract Task<bool> CancelBookingAsync(string pmsBookingId, string? reason = null, CancellationToken ct = default);

    public virtual Task<PMSCredential?> RefreshTokenIfNeededAsync(PMSCredential credential, CancellationToken ct = default)
    {
        // Default: no-op. Override in OAuth-based adapters (Mews, Opera Cloud).
        return Task.FromResult<PMSCredential?>(null);
    }

    // ─── Protected helpers ────────────────────────────────────────────────────

    /// <summary>
    /// Override to set base URL, auth headers, timeouts, etc.
    /// </summary>
    protected virtual void ConfigureHttpClient(HttpClient client, PMSCredential credential)
    {
        if (!string.IsNullOrEmpty(credential.ApiUrl))
            client.BaseAddress = new Uri(credential.ApiUrl.TrimEnd('/') + "/");

        client.Timeout = TimeSpan.FromSeconds(30);
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    protected string DecryptField(string? encrypted)
    {
        if (string.IsNullOrEmpty(encrypted)) return string.Empty;
        return EncryptionService.Decrypt(encrypted);
    }

    protected StringContent JsonContent<T>(T obj)
    {
        var json = JsonSerializer.Serialize(obj, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    protected async Task<T?> GetAsync<T>(string endpoint, CancellationToken ct)
    {
        EnsureInitialized();
        try
        {
            var response = await HttpClient!.GetAsync(endpoint, ct);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync(ct);
            return JsonSerializer.Deserialize<T>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (HttpRequestException ex)
        {
            Logger.LogError(ex, "HTTP GET {Endpoint} failed on {Pms}", endpoint, PmsName);
            throw;
        }
    }

    protected async Task<T?> PostAsync<T>(string endpoint, object body, CancellationToken ct)
    {
        EnsureInitialized();
        try
        {
            var response = await HttpClient!.PostAsync(endpoint, JsonContent(body), ct);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync(ct);
            return JsonSerializer.Deserialize<T>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (HttpRequestException ex)
        {
            Logger.LogError(ex, "HTTP POST {Endpoint} failed on {Pms}", endpoint, PmsName);
            throw;
        }
    }

    /// <summary>
    /// Retries an async operation with exponential back-off.
    /// </summary>
    protected async Task<T> WithRetryAsync<T>(
        Func<Task<T>> operation,
        int maxAttempts = 3,
        CancellationToken ct = default)
    {
        var delay = TimeSpan.FromSeconds(2);
        Exception? lastEx = null;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                lastEx = ex;
                Logger.LogWarning("Attempt {Attempt}/{Max} failed for {Pms}: {Msg}",
                    attempt, maxAttempts, PmsName, ex.Message);
                await Task.Delay(delay, ct);
                delay *= 2;
            }
        }

        throw lastEx!;
    }

    protected void EnsureInitialized()
    {
        if (Credential == null || HttpClient == null)
            throw new InvalidOperationException(
                $"Adapter {PmsName} not initialised. Call Initialize() first.");
    }

    /// <summary>
    /// Maps common status strings from PMS APIs to our BookingStatus enum.
    /// </summary>
    protected static BookingStatus MapStatus(string? rawStatus) => rawStatus?.ToLowerInvariant() switch
    {
        "confirmed" or "reserved" or "active" => BookingStatus.Confirmed,
        "checkedin" or "checked_in" or "in_house" => BookingStatus.CheckedIn,
        "checkedout" or "checked_out" or "departed" => BookingStatus.CheckedOut,
        "cancelled" or "canceled" or "voided" => BookingStatus.Cancelled,
        "noshow" or "no_show" => BookingStatus.NoShow,
        _ => BookingStatus.Pending
    };
}
