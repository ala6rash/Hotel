using HotelBookingSaaS.API.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HotelBookingSaaS.API.Models;

/// <summary>
/// Represents a hotel in the system. Each hotel can have its own PMS and OTA connections.
/// </summary>
[Table("Hotels")]
public class Hotel
{
    [Key]
    public int HotelId { get; set; }

    [Required, MaxLength(200)]
    public string HotelName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Address { get; set; }

    [Required, MaxLength(100)]
    public string City { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Country { get; set; } = "Jordan";

    [MaxLength(20)]
    public string? Phone { get; set; }

    [MaxLength(200)]
    public string? Email { get; set; }

    public int TotalRooms { get; set; }

    /// <summary>
    /// The primary PMS this hotel uses. Can be changed during onboarding.
    /// </summary>
    public PmsType PmsType { get; set; } = PmsType.Manual;

    public bool IsActive { get; set; } = true;

    [MaxLength(500)]
    public string? LogoUrl { get; set; }

    /// <summary>
    /// Channex property ID — assigned after connecting via Channex API.
    /// </summary>
    [MaxLength(100)]
    public string? ChannexPropertyId { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedDate { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<Room> Rooms { get; set; } = new List<Room>();
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public ICollection<OTACredential> OTACredentials { get; set; } = new List<OTACredential>();
    public ICollection<PMSCredential> PMSCredentials { get; set; } = new List<PMSCredential>();
    public ICollection<SyncLog> SyncLogs { get; set; } = new List<SyncLog>();
    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}
