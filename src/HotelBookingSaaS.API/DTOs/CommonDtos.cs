using HotelBookingSaaS.API.Enums;

namespace HotelBookingSaaS.API.DTOs;

// ─── Standard booking DTO shared across all adapters ──────────────────────────

public class StandardBookingDto
{
    public string ExternalBookingId { get; set; } = string.Empty;
    public OTAType OTAType { get; set; }
    public BookingSource Source { get; set; }
    public string GuestName { get; set; } = string.Empty;
    public string? GuestEmail { get; set; }
    public string? GuestPhone { get; set; }
    public string? GuestNationality { get; set; }
    public DateTime CheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }
    public int Adults { get; set; } = 1;
    public int Children { get; set; } = 0;
    public string? RoomType { get; set; }
    public string? RoomNumber { get; set; }
    public decimal TotalPrice { get; set; }
    public string Currency { get; set; } = "JOD";
    public string? RatePlan { get; set; }
    public BookingStatus Status { get; set; } = BookingStatus.Confirmed;
    public string? SpecialRequests { get; set; }
    public DateTime BookingDate { get; set; } = DateTime.UtcNow;
    public string? PmsConfirmationNumber { get; set; }
    public Dictionary<string, string> RawData { get; set; } = new();
}

public class StandardRoomAvailabilityDto
{
    public string RoomTypeId { get; set; } = string.Empty;
    public string RoomTypeName { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public int AvailableCount { get; set; }
    public decimal Price { get; set; }
    public string Currency { get; set; } = "JOD";
}

// ─── API request/response DTOs ─────────────────────────────────────────────────

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public T? Data { get; set; }
    public List<string> Errors { get; set; } = new();

    public static ApiResponse<T> Ok(T data, string? message = null) =>
        new() { Success = true, Data = data, Message = message };

    public static ApiResponse<T> Fail(string error) =>
        new() { Success = false, Errors = new List<string> { error } };

    public static ApiResponse<T> Fail(List<string> errors) =>
        new() { Success = false, Errors = errors };
}

public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}

// ─── Hotel DTOs ────────────────────────────────────────────────────────────────

public class HotelCreateDto
{
    public string HotelName { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = "Jordan";
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public int TotalRooms { get; set; }
    public PmsType PmsType { get; set; } = PmsType.Manual;
}

public class HotelUpdateDto
{
    public string? HotelName { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public int? TotalRooms { get; set; }
    public PmsType? PmsType { get; set; }
    public bool? IsActive { get; set; }
}

public class HotelResponseDto
{
    public int HotelId { get; set; }
    public string HotelName { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public int TotalRooms { get; set; }
    public PmsType PmsType { get; set; }
    public string PmsTypeName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string? ChannexPropertyId { get; set; }
    public DateTime CreatedDate { get; set; }
    public int ConnectedOTAs { get; set; }
    public bool PmsConnected { get; set; }
}

// ─── Booking DTOs ──────────────────────────────────────────────────────────────

public class BookingQueryDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int? HotelId { get; set; }
    public OTAType? OTAType { get; set; }
    public BookingStatus? Status { get; set; }
    public DateTime? CheckInFrom { get; set; }
    public DateTime? CheckInTo { get; set; }
    public string? GuestName { get; set; }
    public string? SortBy { get; set; } = "CheckInDate";
    public bool SortDesc { get; set; } = false;
}

public class BookingResponseDto
{
    public int BookingId { get; set; }
    public int HotelId { get; set; }
    public string HotelName { get; set; } = string.Empty;
    public string? OTABookingId { get; set; }
    public OTAType OTAType { get; set; }
    public string OTATypeName { get; set; } = string.Empty;
    public string GuestName { get; set; } = string.Empty;
    public string? GuestEmail { get; set; }
    public string? GuestPhone { get; set; }
    public DateTime CheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }
    public int Nights { get; set; }
    public string? RoomType { get; set; }
    public decimal TotalPrice { get; set; }
    public string Currency { get; set; } = string.Empty;
    public BookingStatus Status { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
}

// ─── Credentials DTOs ─────────────────────────────────────────────────────────

public class OTACredentialCreateDto
{
    public int HotelId { get; set; }
    public OTAType OTAType { get; set; }
    public string? PropertyId { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? ApiKey { get; set; }
    public bool IsViaChannex { get; set; } = true;
    public string? ChannexChannelId { get; set; }
}

public class PMSCredentialCreateDto
{
    public int HotelId { get; set; }
    public PmsType PmsType { get; set; }
    public string? ApiUrl { get; set; }
    public string? ApiKey { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? AdditionalConfigJson { get; set; }
}

public class CredentialTestResultDto
{
    public bool IsConnected { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime TestedAt { get; set; } = DateTime.UtcNow;
    public int? BookingsFound { get; set; }
}

// ─── Auth DTOs ─────────────────────────────────────────────────────────────────

public class LoginDto
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LoginResponseDto
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public int UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public int? HotelId { get; set; }
}

// ─── Onboarding DTOs ──────────────────────────────────────────────────────────

public class OnboardingStartDto
{
    public HotelCreateDto Hotel { get; set; } = new();
    public string AdminEmail { get; set; } = string.Empty;
    public string AdminPassword { get; set; } = string.Empty;
    public string AdminFirstName { get; set; } = string.Empty;
    public string AdminLastName { get; set; } = string.Empty;
}

public class OnboardingStatusDto
{
    public int HotelId { get; set; }
    public string HotelName { get; set; } = string.Empty;
    public bool HotelCreated { get; set; }
    public bool PmsConnected { get; set; }
    public bool ChannexConnected { get; set; }
    public List<string> ConnectedOTAs { get; set; } = new();
    public bool IsComplete { get; set; }
    public int CompletionPercentage { get; set; }
}
