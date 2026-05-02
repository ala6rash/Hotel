using HotelBookingSaaS.API.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HotelBookingSaaS.API.Models;

/// <summary>
/// Stores encrypted PMS credentials for hotel property management systems.
/// All sensitive fields are AES-256 encrypted at rest.
/// </summary>
[Table("PMSCredentials")]
public class PMSCredential
{
    [Key]
    public int CredentialId { get; set; }

    [ForeignKey(nameof(Hotel))]
    public int HotelId { get; set; }

    public PmsType PmsType { get; set; }

    [MaxLength(500)]
    public string? ApiUrl { get; set; }

    // Encrypted credential fields
    [MaxLength(2000)]
    public string? ApiKeyEncrypted { get; set; }

    [MaxLength(1000)]
    public string? ClientIdEncrypted { get; set; }

    [MaxLength(2000)]
    public string? ClientSecretEncrypted { get; set; }

    [MaxLength(1000)]
    public string? UsernameEncrypted { get; set; }

    [MaxLength(1000)]
    public string? PasswordEncrypted { get; set; }

    [MaxLength(4000)]
    public string? AccessTokenEncrypted { get; set; }

    [MaxLength(4000)]
    public string? RefreshTokenEncrypted { get; set; }

    public DateTime? TokenExpiresAt { get; set; }

    /// <summary>
    /// Additional PMS-specific config stored as JSON (e.g., Opera hotel code, Mews enterprise ID).
    /// </summary>
    [MaxLength(4000)]
    public string? AdditionalConfigJson { get; set; }

    public bool IsActive { get; set; } = true;

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
