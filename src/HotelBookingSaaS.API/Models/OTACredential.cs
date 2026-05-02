using HotelBookingSaaS.API.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HotelBookingSaaS.API.Models;

/// <summary>
/// Stores encrypted credentials for OTA platforms (Booking.com, Airbnb, etc).
/// All sensitive fields are stored AES-256 encrypted. Never store plaintext.
/// </summary>
[Table("OTACredentials")]
public class OTACredential
{
    [Key]
    public int CredentialId { get; set; }

    [ForeignKey(nameof(Hotel))]
    public int HotelId { get; set; }

    public OTAType OTAType { get; set; }

    /// <summary>
    /// The property/listing ID as used by the OTA platform.
    /// </summary>
    [MaxLength(200)]
    public string? PropertyId { get; set; }

    // All credential fields stored encrypted (AES-256)
    [MaxLength(1000)]
    public string? UsernameEncrypted { get; set; }

    [MaxLength(1000)]
    public string? PasswordEncrypted { get; set; }

    [MaxLength(2000)]
    public string? ApiKeyEncrypted { get; set; }

    [MaxLength(4000)]
    public string? OAuthTokenEncrypted { get; set; }

    [MaxLength(4000)]
    public string? RefreshTokenEncrypted { get; set; }

    public DateTime? TokenExpiresAt { get; set; }

    /// <summary>
    /// Channex channel ID — used when this OTA is connected via Channex.
    /// </summary>
    [MaxLength(200)]
    public string? ChannexChannelId { get; set; }

    public bool IsActive { get; set; } = true;
    public bool IsViaChannex { get; set; } = true;

    public DateTime? ConnectedDate { get; set; }
    public DateTime? LastTestedDate { get; set; }
    public bool? LastTestResult { get; set; }

    [MaxLength(500)]
    public string? LastTestError { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedDate { get; set; } = DateTime.UtcNow;

    // Navigation
    public Hotel Hotel { get; set; } = null!;
}
