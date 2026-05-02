using HotelBookingSaaS.API.Data;
using HotelBookingSaaS.API.DTOs;
using HotelBookingSaaS.API.Enums;
using HotelBookingSaaS.API.Models;
using HotelBookingSaaS.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelBookingSaaS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "SuperAdmin,HotelAdmin")]
public class CredentialsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IEncryptionService _encryption;
    private readonly IPmsAdapterFactory _pmsFactory;
    private readonly Adapters.Implementations.ChannexAdapter _channexAdapter;
    private readonly ILogger<CredentialsController> _logger;

    public CredentialsController(
        ApplicationDbContext db,
        IEncryptionService encryption,
        IPmsAdapterFactory pmsFactory,
        Adapters.Implementations.ChannexAdapter channexAdapter,
        ILogger<CredentialsController> logger)
    {
        _db = db;
        _encryption = encryption;
        _pmsFactory = pmsFactory;
        _channexAdapter = channexAdapter;
        _logger = logger;
    }

    // ─── OTA Credentials ──────────────────────────────────────────────────────

    [HttpGet("ota/{hotelId:int}")]
    public async Task<ActionResult<ApiResponse<List<object>>>> GetOTACredentials(int hotelId)
    {
        if (!CanAccessHotel(hotelId)) return Forbid();

        var creds = await _db.OTACredentials
            .Where(c => c.HotelId == hotelId)
            .Select(c => (object)new
            {
                c.CredentialId,
                c.HotelId,
                OTAType = c.OTAType.ToString(),
                c.PropertyId,
                c.IsActive,
                c.IsViaChannex,
                c.ChannexChannelId,
                c.ConnectedDate,
                c.LastTestedDate,
                c.LastTestResult,
                c.LastTestError
                // Never return encrypted fields
            })
            .ToListAsync();

        return Ok(ApiResponse<List<object>>.Ok(creds));
    }

    [HttpPost("ota")]
    public async Task<ActionResult<ApiResponse<bool>>> SaveOTACredential(OTACredentialCreateDto dto)
    {
        if (!CanAccessHotel(dto.HotelId)) return Forbid();

        var existing = await _db.OTACredentials
            .FirstOrDefaultAsync(c => c.HotelId == dto.HotelId && c.OTAType == dto.OTAType);

        if (existing == null)
        {
            existing = new OTACredential
            {
                HotelId = dto.HotelId,
                OTAType = dto.OTAType,
                ConnectedDate = DateTime.UtcNow
            };
            _db.OTACredentials.Add(existing);
        }

        // Encrypt all sensitive fields
        existing.PropertyId = dto.PropertyId;
        existing.UsernameEncrypted = dto.Username != null ? _encryption.Encrypt(dto.Username) : null;
        existing.PasswordEncrypted = dto.Password != null ? _encryption.Encrypt(dto.Password) : null;
        existing.ApiKeyEncrypted = dto.ApiKey != null ? _encryption.Encrypt(dto.ApiKey) : null;
        existing.IsViaChannex = dto.IsViaChannex;
        existing.ChannexChannelId = dto.ChannexChannelId;
        existing.IsActive = true;
        existing.UpdatedDate = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        _logger.LogInformation("OTA credential saved for hotel {HotelId}, OTA {OTAType}",
            dto.HotelId, dto.OTAType);

        return Ok(ApiResponse<bool>.Ok(true, "OTA credential saved and encrypted"));
    }

    [HttpPost("ota/{credentialId:int}/test")]
    public async Task<ActionResult<ApiResponse<CredentialTestResultDto>>> TestOTACredential(int credentialId)
    {
        var cred = await _db.OTACredentials.FindAsync(credentialId);
        if (cred == null)
            return NotFound(ApiResponse<CredentialTestResultDto>.Fail("Credential not found"));

        if (!CanAccessHotel(cred.HotelId)) return Forbid();

        _channexAdapter.Initialize(cred);
        var result = await _channexAdapter.TestConnectionAsync();

        // Update test result
        cred.LastTestedDate = DateTime.UtcNow;
        cred.LastTestResult = result.IsConnected;
        cred.LastTestError = result.IsConnected ? null : result.Message;
        await _db.SaveChangesAsync();

        return Ok(ApiResponse<CredentialTestResultDto>.Ok(result));
    }

    [HttpDelete("ota/{credentialId:int}")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteOTACredential(int credentialId)
    {
        var cred = await _db.OTACredentials.FindAsync(credentialId);
        if (cred == null)
            return NotFound(ApiResponse<bool>.Fail("Credential not found"));

        if (!CanAccessHotel(cred.HotelId)) return Forbid();

        cred.IsActive = false;
        await _db.SaveChangesAsync();
        return Ok(ApiResponse<bool>.Ok(true));
    }

    // ─── PMS Credentials ──────────────────────────────────────────────────────

    [HttpGet("pms/{hotelId:int}")]
    public async Task<ActionResult<ApiResponse<List<object>>>> GetPMSCredentials(int hotelId)
    {
        if (!CanAccessHotel(hotelId)) return Forbid();

        var creds = await _db.PMSCredentials
            .Where(c => c.HotelId == hotelId)
            .Select(c => (object)new
            {
                c.CredentialId,
                c.HotelId,
                PmsType = c.PmsType.ToString(),
                c.ApiUrl,
                c.IsActive,
                c.ConnectedDate,
                c.LastTestedDate,
                c.LastTestResult,
                c.LastTestError,
                c.TokenExpiresAt
                // Never return encrypted fields
            })
            .ToListAsync();

        return Ok(ApiResponse<List<object>>.Ok(creds));
    }

    [HttpPost("pms")]
    public async Task<ActionResult<ApiResponse<bool>>> SavePMSCredential(PMSCredentialCreateDto dto)
    {
        if (!CanAccessHotel(dto.HotelId)) return Forbid();

        var existing = await _db.PMSCredentials
            .FirstOrDefaultAsync(c => c.HotelId == dto.HotelId && c.PmsType == dto.PmsType);

        if (existing == null)
        {
            existing = new PMSCredential
            {
                HotelId = dto.HotelId,
                PmsType = dto.PmsType,
                ConnectedDate = DateTime.UtcNow
            };
            _db.PMSCredentials.Add(existing);
        }

        existing.ApiUrl = dto.ApiUrl;
        existing.ApiKeyEncrypted = dto.ApiKey != null ? _encryption.Encrypt(dto.ApiKey) : null;
        existing.ClientIdEncrypted = dto.ClientId != null ? _encryption.Encrypt(dto.ClientId) : null;
        existing.ClientSecretEncrypted = dto.ClientSecret != null ? _encryption.Encrypt(dto.ClientSecret) : null;
        existing.UsernameEncrypted = dto.Username != null ? _encryption.Encrypt(dto.Username) : null;
        existing.PasswordEncrypted = dto.Password != null ? _encryption.Encrypt(dto.Password) : null;
        existing.AdditionalConfigJson = dto.AdditionalConfigJson;
        existing.IsActive = true;
        existing.UpdatedDate = DateTime.UtcNow;

        // Also update hotel's PmsType
        var hotel = await _db.Hotels.FindAsync(dto.HotelId);
        if (hotel != null) hotel.PmsType = dto.PmsType;

        await _db.SaveChangesAsync();
        _logger.LogInformation("PMS credential saved for hotel {HotelId}, PMS {PmsType}",
            dto.HotelId, dto.PmsType);

        return Ok(ApiResponse<bool>.Ok(true, "PMS credential saved and encrypted"));
    }

    [HttpPost("pms/{credentialId:int}/test")]
    public async Task<ActionResult<ApiResponse<CredentialTestResultDto>>> TestPMSCredential(int credentialId)
    {
        var cred = await _db.PMSCredentials.FindAsync(credentialId);
        if (cred == null)
            return NotFound(ApiResponse<CredentialTestResultDto>.Fail("Credential not found"));

        if (!CanAccessHotel(cred.HotelId)) return Forbid();

        try
        {
            var adapter = _pmsFactory.Create(cred);
            var result = await adapter.TestConnectionAsync();

            cred.LastTestedDate = DateTime.UtcNow;
            cred.LastTestResult = result.IsConnected;
            cred.LastTestError = result.IsConnected ? null : result.Message;
            await _db.SaveChangesAsync();

            return Ok(ApiResponse<CredentialTestResultDto>.Ok(result));
        }
        catch (NotSupportedException ex)
        {
            return BadRequest(ApiResponse<CredentialTestResultDto>.Fail(ex.Message));
        }
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private bool CanAccessHotel(int hotelId)
    {
        if (User.IsInRole("SuperAdmin")) return true;
        var claim = User.FindFirst("hotelId")?.Value;
        return int.TryParse(claim, out var id) && id == hotelId;
    }
}
