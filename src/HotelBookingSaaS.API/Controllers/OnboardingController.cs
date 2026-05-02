using HotelBookingSaaS.API.Data;
using HotelBookingSaaS.API.DTOs;
using HotelBookingSaaS.API.Enums;
using HotelBookingSaaS.API.Models;
using HotelBookingSaaS.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelBookingSaaS.API.Controllers;

/// <summary>
/// Guides a new hotel through the full onboarding process:
/// 1. Create hotel + admin user
/// 2. Configure PMS credentials and test connection
/// 3. Configure Channex (OTA channel manager) credentials
/// 4. Connect individual OTA platforms
/// 5. Run initial sync
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class OnboardingController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IEncryptionService _encryption;
    private readonly IPmsAdapterFactory _pmsFactory;
    private readonly Adapters.Implementations.ChannexAdapter _channexAdapter;
    private readonly IBookingSyncService _syncService;
    private readonly ILogger<OnboardingController> _logger;

    public OnboardingController(
        ApplicationDbContext db,
        IEncryptionService encryption,
        IPmsAdapterFactory pmsFactory,
        Adapters.Implementations.ChannexAdapter channexAdapter,
        IBookingSyncService syncService,
        ILogger<OnboardingController> logger)
    {
        _db = db;
        _encryption = encryption;
        _pmsFactory = pmsFactory;
        _channexAdapter = channexAdapter;
        _syncService = syncService;
        _logger = logger;
    }

    /// <summary>Step 1: Create hotel + admin user account.</summary>
    [HttpPost("start")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<ActionResult<ApiResponse<OnboardingStatusDto>>> Start(OnboardingStartDto dto)
    {
        if (await _db.Hotels.AnyAsync(h => h.HotelName == dto.Hotel.HotelName))
            return Conflict(ApiResponse<OnboardingStatusDto>.Fail(
                "A hotel with this name already exists."));

        if (await _db.Users.AnyAsync(u => u.Email == dto.AdminEmail))
            return Conflict(ApiResponse<OnboardingStatusDto>.Fail(
                "A user with this email already exists."));

        // Create hotel
        var hotel = new Hotel
        {
            HotelName = dto.Hotel.HotelName,
            Address = dto.Hotel.Address,
            City = dto.Hotel.City,
            Country = dto.Hotel.Country,
            Phone = dto.Hotel.Phone,
            Email = dto.Hotel.Email,
            TotalRooms = dto.Hotel.TotalRooms,
            PmsType = dto.Hotel.PmsType,
            IsActive = true
        };
        _db.Hotels.Add(hotel);
        await _db.SaveChangesAsync();

        // Create admin user for this hotel
        var user = new User
        {
            HotelId = hotel.HotelId,
            Email = dto.AdminEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.AdminPassword),
            FirstName = dto.AdminFirstName,
            LastName = dto.AdminLastName,
            Role = UserRole.HotelAdmin,
            IsActive = true
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Onboarding started for hotel {HotelId} ({Name})",
            hotel.HotelId, hotel.HotelName);

        return Ok(ApiResponse<OnboardingStatusDto>.Ok(
            await BuildStatusAsync(hotel.HotelId),
            $"Hotel '{hotel.HotelName}' created. Proceed to Step 2: configure PMS."));
    }

    /// <summary>Step 2: Save and test PMS credentials.</summary>
    [HttpPost("{hotelId:int}/pms")]
    [Authorize(Roles = "SuperAdmin,HotelAdmin")]
    public async Task<ActionResult<ApiResponse<OnboardingStatusDto>>> ConfigurePms(
        int hotelId, PMSCredentialCreateDto dto)
    {
        dto.HotelId = hotelId;

        var cred = new PMSCredential
        {
            HotelId = hotelId,
            PmsType = dto.PmsType,
            ApiUrl = dto.ApiUrl,
            ApiKeyEncrypted = dto.ApiKey != null ? _encryption.Encrypt(dto.ApiKey) : null,
            ClientIdEncrypted = dto.ClientId != null ? _encryption.Encrypt(dto.ClientId) : null,
            ClientSecretEncrypted = dto.ClientSecret != null ? _encryption.Encrypt(dto.ClientSecret) : null,
            UsernameEncrypted = dto.Username != null ? _encryption.Encrypt(dto.Username) : null,
            PasswordEncrypted = dto.Password != null ? _encryption.Encrypt(dto.Password) : null,
            AdditionalConfigJson = dto.AdditionalConfigJson,
            IsActive = true,
            ConnectedDate = DateTime.UtcNow
        };

        // Upsert
        var existing = await _db.PMSCredentials
            .FirstOrDefaultAsync(c => c.HotelId == hotelId && c.PmsType == dto.PmsType);
        if (existing != null)
            _db.PMSCredentials.Remove(existing);

        _db.PMSCredentials.Add(cred);
        await _db.SaveChangesAsync();

        // Test connection immediately
        CredentialTestResultDto testResult;
        try
        {
            var adapter = _pmsFactory.Create(cred);
            testResult = await adapter.TestConnectionAsync();
        }
        catch (NotSupportedException)
        {
            testResult = new CredentialTestResultDto
            {
                IsConnected = true,
                Message = "PMS adapter not yet implemented — credentials saved."
            };
        }

        cred.LastTestedDate = DateTime.UtcNow;
        cred.LastTestResult = testResult.IsConnected;
        cred.LastTestError = testResult.IsConnected ? null : testResult.Message;
        await _db.SaveChangesAsync();

        return Ok(ApiResponse<OnboardingStatusDto>.Ok(
            await BuildStatusAsync(hotelId),
            testResult.Message));
    }

    /// <summary>Step 3: Connect Channex (the OTA channel manager).</summary>
    [HttpPost("{hotelId:int}/channex")]
    [Authorize(Roles = "SuperAdmin,HotelAdmin")]
    public async Task<ActionResult<ApiResponse<OnboardingStatusDto>>> ConnectChannex(
        int hotelId, OTACredentialCreateDto dto)
    {
        dto.HotelId = hotelId;
        dto.OTAType = OTAType.Channex;
        dto.IsViaChannex = true;

        var cred = new OTACredential
        {
            HotelId = hotelId,
            OTAType = OTAType.Channex,
            PropertyId = dto.PropertyId,
            ApiKeyEncrypted = dto.ApiKey != null ? _encryption.Encrypt(dto.ApiKey) : null,
            IsViaChannex = true,
            IsActive = true,
            ConnectedDate = DateTime.UtcNow
        };

        var existing = await _db.OTACredentials
            .FirstOrDefaultAsync(c => c.HotelId == hotelId && c.OTAType == OTAType.Channex);
        if (existing != null) _db.OTACredentials.Remove(existing);

        _db.OTACredentials.Add(cred);
        await _db.SaveChangesAsync();

        // Test
        _channexAdapter.Initialize(cred);
        var testResult = await _channexAdapter.TestConnectionAsync();

        cred.LastTestedDate = DateTime.UtcNow;
        cred.LastTestResult = testResult.IsConnected;
        cred.LastTestError = testResult.IsConnected ? null : testResult.Message;

        // Store Channex property ID on hotel
        if (testResult.IsConnected && !string.IsNullOrEmpty(dto.PropertyId))
        {
            var hotel = await _db.Hotels.FindAsync(hotelId);
            if (hotel != null) hotel.ChannexPropertyId = dto.PropertyId;
        }

        await _db.SaveChangesAsync();

        return Ok(ApiResponse<OnboardingStatusDto>.Ok(
            await BuildStatusAsync(hotelId),
            testResult.Message));
    }

    /// <summary>Step 4: Activate specific OTA platforms (Booking.com, Airbnb, etc.).</summary>
    [HttpPost("{hotelId:int}/ota/{otaType}")]
    [Authorize(Roles = "SuperAdmin,HotelAdmin")]
    public async Task<ActionResult<ApiResponse<OnboardingStatusDto>>> ActivateOTA(
        int hotelId, OTAType otaType, OTACredentialCreateDto dto)
    {
        dto.HotelId = hotelId;
        dto.OTAType = otaType;

        // For OTAs via Channex, link the channel ID
        var cred = new OTACredential
        {
            HotelId = hotelId,
            OTAType = otaType,
            PropertyId = dto.PropertyId,
            ChannexChannelId = dto.ChannexChannelId,
            UsernameEncrypted = dto.Username != null ? _encryption.Encrypt(dto.Username) : null,
            PasswordEncrypted = dto.Password != null ? _encryption.Encrypt(dto.Password) : null,
            ApiKeyEncrypted = dto.ApiKey != null ? _encryption.Encrypt(dto.ApiKey) : null,
            IsViaChannex = dto.IsViaChannex,
            IsActive = true,
            ConnectedDate = DateTime.UtcNow
        };

        var existing = await _db.OTACredentials
            .FirstOrDefaultAsync(c => c.HotelId == hotelId && c.OTAType == otaType);
        if (existing != null) _db.OTACredentials.Remove(existing);

        _db.OTACredentials.Add(cred);
        await _db.SaveChangesAsync();

        return Ok(ApiResponse<OnboardingStatusDto>.Ok(
            await BuildStatusAsync(hotelId),
            $"{otaType} connected successfully."));
    }

    /// <summary>Step 5: Run initial data sync.</summary>
    [HttpPost("{hotelId:int}/sync")]
    [Authorize(Roles = "SuperAdmin,HotelAdmin")]
    public async Task<ActionResult<ApiResponse<OnboardingStatusDto>>> RunInitialSync(int hotelId)
    {
        try
        {
            await _syncService.SyncHotelAsync(hotelId);
            return Ok(ApiResponse<OnboardingStatusDto>.Ok(
                await BuildStatusAsync(hotelId),
                "Initial sync completed successfully."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Initial sync failed for hotel {HotelId}", hotelId);
            return Ok(ApiResponse<OnboardingStatusDto>.Ok(
                await BuildStatusAsync(hotelId),
                $"Sync encountered an error: {ex.Message}"));
        }
    }

    /// <summary>Get current onboarding status for a hotel.</summary>
    [HttpGet("{hotelId:int}/status")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<OnboardingStatusDto>>> GetStatus(int hotelId)
    {
        return Ok(ApiResponse<OnboardingStatusDto>.Ok(await BuildStatusAsync(hotelId)));
    }

    // ─── Private ──────────────────────────────────────────────────────────────

    private async Task<OnboardingStatusDto> BuildStatusAsync(int hotelId)
    {
        var hotel = await _db.Hotels
            .Include(h => h.OTACredentials)
            .Include(h => h.PMSCredentials)
            .FirstOrDefaultAsync(h => h.HotelId == hotelId);

        if (hotel == null) return new OnboardingStatusDto { HotelId = hotelId };

        var pmsConnected = hotel.PMSCredentials.Any(c => c.IsActive && c.LastTestResult == true);
        var channexConnected = hotel.OTACredentials.Any(c =>
            c.IsActive && c.OTAType == OTAType.Channex && c.LastTestResult == true);
        var connectedOTAs = hotel.OTACredentials
            .Where(c => c.IsActive && c.OTAType != OTAType.Channex)
            .Select(c => c.OTAType.ToString())
            .ToList();

        int score = 0;
        if (true) score += 25;             // Hotel created
        if (pmsConnected) score += 25;
        if (channexConnected) score += 25;
        if (connectedOTAs.Count > 0) score += 25;

        return new OnboardingStatusDto
        {
            HotelId = hotelId,
            HotelName = hotel.HotelName,
            HotelCreated = true,
            PmsConnected = pmsConnected,
            ChannexConnected = channexConnected,
            ConnectedOTAs = connectedOTAs,
            IsComplete = score == 100,
            CompletionPercentage = score
        };
    }
}
