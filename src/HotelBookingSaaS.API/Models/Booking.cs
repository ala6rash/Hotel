using HotelBookingSaaS.API.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HotelBookingSaaS.API.Models;

[Table("Bookings")]
public class Booking
{
    [Key]
    public int BookingId { get; set; }

    [ForeignKey(nameof(Hotel))]
    public int HotelId { get; set; }

    /// <summary>
    /// The booking ID as it appears in the OTA or PMS system.
    /// </summary>
    [MaxLength(100)]
    public string? OTABookingId { get; set; }

    /// <summary>
    /// Internal PMS confirmation number for cross-referencing.
    /// </summary>
    [MaxLength(100)]
    public string? PmsConfirmationNumber { get; set; }

    public OTAType OTAType { get; set; } = OTAType.Direct;
    public BookingSource Source { get; set; } = BookingSource.Direct;

    // Guest info
    [Required, MaxLength(200)]
    public string GuestName { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? GuestEmail { get; set; }

    [MaxLength(30)]
    public string? GuestPhone { get; set; }

    [MaxLength(100)]
    public string? GuestNationality { get; set; }

    // Stay details
    public DateTime CheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }

    public int Adults { get; set; } = 1;
    public int Children { get; set; } = 0;

    [MaxLength(200)]
    public string? RoomType { get; set; }

    [MaxLength(100)]
    public string? RoomNumber { get; set; }

    // Pricing
    [Column(TypeName = "decimal(10,2)")]
    public decimal TotalPrice { get; set; }

    [MaxLength(3)]
    public string Currency { get; set; } = "JOD";

    [MaxLength(100)]
    public string? RatePlan { get; set; }

    // Status
    public BookingStatus BookingStatus { get; set; } = BookingStatus.Confirmed;

    [MaxLength(1000)]
    public string? SpecialRequests { get; set; }

    [MaxLength(500)]
    public string? CancellationReason { get; set; }

    public DateTime? CancelledDate { get; set; }

    // Timestamps
    public DateTime BookingDate { get; set; } = DateTime.UtcNow;
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedDate { get; set; } = DateTime.UtcNow;

    // Navigation
    public Hotel Hotel { get; set; } = null!;
}
