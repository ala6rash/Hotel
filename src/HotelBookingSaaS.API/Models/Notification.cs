using HotelBookingSaaS.API.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HotelBookingSaaS.API.Models;

[Table("Notifications")]
public class Notification
{
    [Key]
    public int NotificationId { get; set; }

    [ForeignKey(nameof(Hotel))]
    public int HotelId { get; set; }

    public NotificationType Type { get; set; }

    [Required, MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [Required, MaxLength(2000)]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// JSON payload with notification-specific data (e.g., booking details).
    /// </summary>
    [MaxLength(4000)]
    public string? DataJson { get; set; }

    public bool IsRead { get; set; } = false;

    public int? RelatedBookingId { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? ReadDate { get; set; }

    // Navigation
    public Hotel Hotel { get; set; } = null!;
}
