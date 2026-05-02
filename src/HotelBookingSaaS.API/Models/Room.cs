using HotelBookingSaaS.API.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HotelBookingSaaS.API.Models;

[Table("Rooms")]
public class Room
{
    [Key]
    public int RoomId { get; set; }

    [ForeignKey(nameof(Hotel))]
    public int HotelId { get; set; }

    [Required, MaxLength(100)]
    public string RoomNumber { get; set; } = string.Empty;

    public RoomType RoomType { get; set; } = RoomType.Double;

    [MaxLength(200)]
    public string? RoomTypeName { get; set; }

    public int Capacity { get; set; } = 2;

    public int TotalCount { get; set; } = 1;

    [Column(TypeName = "decimal(10,2)")]
    public decimal BasePrice { get; set; }

    [MaxLength(3)]
    public string Currency { get; set; } = "JOD";

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// External room ID from the PMS, used for mapping when syncing.
    /// </summary>
    [MaxLength(100)]
    public string? ExternalRoomId { get; set; }

    [MaxLength(100)]
    public string? ChannexRoomTypeId { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    // Navigation
    public Hotel Hotel { get; set; } = null!;
}
