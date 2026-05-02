using HotelBookingSaaS.API.Data;
using HotelBookingSaaS.API.DTOs;
using HotelBookingSaaS.API.Enums;
using HotelBookingSaaS.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelBookingSaaS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class HotelsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<HotelsController> _logger;

    public HotelsController(ApplicationDbContext db, ILogger<HotelsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>List all hotels. SuperAdmin sees all; hotel users see only their own.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<HotelResponseDto>>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool? isActive = null)
    {
        var query = _db.Hotels
            .Include(h => h.OTACredentials)
            .Include(h => h.PMSCredentials)
            .AsQueryable();

        // Hotel-level users only see their own hotel
        if (!User.IsInRole("SuperAdmin"))
        {
            var hotelId = GetCurrentUserHotelId();
            if (hotelId.HasValue)
                query = query.Where(h => h.HotelId == hotelId.Value);
        }

        if (isActive.HasValue)
            query = query.Where(h => h.IsActive == isActive.Value);

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(h => h.HotelName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(h => ToDto(h))
            .ToListAsync();

        return Ok(ApiResponse<PagedResult<HotelResponseDto>>.Ok(new PagedResult<HotelResponseDto>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        }));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<HotelResponseDto>>> GetById(int id)
    {
        if (!CanAccessHotel(id))
            return Forbid();

        var hotel = await _db.Hotels
            .Include(h => h.OTACredentials)
            .Include(h => h.PMSCredentials)
            .FirstOrDefaultAsync(h => h.HotelId == id);

        if (hotel == null)
            return NotFound(ApiResponse<HotelResponseDto>.Fail($"Hotel {id} not found"));

        return Ok(ApiResponse<HotelResponseDto>.Ok(ToDto(hotel)));
    }

    [HttpPost]
    [Authorize(Roles = "SuperAdmin,HotelAdmin")]
    public async Task<ActionResult<ApiResponse<HotelResponseDto>>> Create(HotelCreateDto dto)
    {
        if (await _db.Hotels.AnyAsync(h => h.HotelName == dto.HotelName))
            return Conflict(ApiResponse<HotelResponseDto>.Fail("A hotel with this name already exists."));

        var hotel = new Hotel
        {
            HotelName = dto.HotelName,
            Address = dto.Address,
            City = dto.City,
            Country = dto.Country,
            Phone = dto.Phone,
            Email = dto.Email,
            TotalRooms = dto.TotalRooms,
            PmsType = dto.PmsType,
            IsActive = true,
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        };

        _db.Hotels.Add(hotel);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Hotel created: {HotelId} {Name}", hotel.HotelId, hotel.HotelName);
        return CreatedAtAction(nameof(GetById), new { id = hotel.HotelId },
            ApiResponse<HotelResponseDto>.Ok(ToDto(hotel), "Hotel created successfully"));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "SuperAdmin,HotelAdmin")]
    public async Task<ActionResult<ApiResponse<HotelResponseDto>>> Update(int id, HotelUpdateDto dto)
    {
        if (!CanAccessHotel(id))
            return Forbid();

        var hotel = await _db.Hotels.FindAsync(id);
        if (hotel == null)
            return NotFound(ApiResponse<HotelResponseDto>.Fail($"Hotel {id} not found"));

        if (dto.HotelName != null) hotel.HotelName = dto.HotelName;
        if (dto.Address != null) hotel.Address = dto.Address;
        if (dto.City != null) hotel.City = dto.City;
        if (dto.Phone != null) hotel.Phone = dto.Phone;
        if (dto.Email != null) hotel.Email = dto.Email;
        if (dto.TotalRooms.HasValue) hotel.TotalRooms = dto.TotalRooms.Value;
        if (dto.PmsType.HasValue) hotel.PmsType = dto.PmsType.Value;
        if (dto.IsActive.HasValue) hotel.IsActive = dto.IsActive.Value;

        await _db.SaveChangesAsync();
        return Ok(ApiResponse<HotelResponseDto>.Ok(ToDto(hotel)));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(int id)
    {
        var hotel = await _db.Hotels.FindAsync(id);
        if (hotel == null)
            return NotFound(ApiResponse<bool>.Fail($"Hotel {id} not found"));

        // Soft delete
        hotel.IsActive = false;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Hotel {HotelId} deactivated", id);
        return Ok(ApiResponse<bool>.Ok(true, "Hotel deactivated"));
    }

    [HttpGet("{id:int}/stats")]
    public async Task<ActionResult<ApiResponse<object>>> GetStats(int id)
    {
        if (!CanAccessHotel(id))
            return Forbid();

        var today = DateTime.UtcNow.Date;
        var stats = new
        {
            TotalBookings = await _db.Bookings.CountAsync(b => b.HotelId == id),
            ActiveToday = await _db.Bookings.CountAsync(b =>
                b.HotelId == id &&
                b.CheckInDate <= today &&
                b.CheckOutDate >= today &&
                b.BookingStatus == BookingStatus.Confirmed),
            CheckinsToday = await _db.Bookings.CountAsync(b =>
                b.HotelId == id &&
                b.CheckInDate == today),
            CheckoutsToday = await _db.Bookings.CountAsync(b =>
                b.HotelId == id &&
                b.CheckOutDate == today),
            LastSyncTime = await _db.SyncLogs
                .Where(s => s.HotelId == id && s.Status == SyncStatus.Success)
                .OrderByDescending(s => s.SyncEndTime)
                .Select(s => s.SyncEndTime)
                .FirstOrDefaultAsync()
        };

        return Ok(ApiResponse<object>.Ok(stats));
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private bool CanAccessHotel(int hotelId)
    {
        if (User.IsInRole("SuperAdmin")) return true;
        var userHotelId = GetCurrentUserHotelId();
        return userHotelId == hotelId;
    }

    private int? GetCurrentUserHotelId()
    {
        var claim = User.FindFirst("hotelId")?.Value;
        return int.TryParse(claim, out var id) ? id : null;
    }

    private static HotelResponseDto ToDto(Hotel h) => new()
    {
        HotelId = h.HotelId,
        HotelName = h.HotelName,
        Address = h.Address,
        City = h.City,
        Country = h.Country,
        Phone = h.Phone,
        Email = h.Email,
        TotalRooms = h.TotalRooms,
        PmsType = h.PmsType,
        PmsTypeName = h.PmsType.ToString(),
        IsActive = h.IsActive,
        ChannexPropertyId = h.ChannexPropertyId,
        CreatedDate = h.CreatedDate,
        ConnectedOTAs = h.OTACredentials.Count(c => c.IsActive),
        PmsConnected = h.PMSCredentials.Any(c => c.IsActive)
    };
}
