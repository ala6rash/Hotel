using HotelBookingSaaS.API.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HotelBookingSaaS.API.Models;

[Table("SyncLogs")]
public class SyncLog
{
    [Key]
    public int LogId { get; set; }

    [ForeignKey(nameof(Hotel))]
    public int HotelId { get; set; }

    public OTAType? OTAType { get; set; }
    public PmsType? PmsType { get; set; }

    public DateTime SyncStartTime { get; set; } = DateTime.UtcNow;
    public DateTime? SyncEndTime { get; set; }

    public SyncStatus Status { get; set; } = SyncStatus.Running;

    public int RecordsProcessed { get; set; } = 0;
    public int RecordsCreated { get; set; } = 0;
    public int RecordsUpdated { get; set; } = 0;
    public int RecordsFailed { get; set; } = 0;

    [MaxLength(2000)]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// JSON details of sync operation for debugging.
    /// </summary>
    [MaxLength(8000)]
    public string? DetailsJson { get; set; }

    /// <summary>
    /// Hangfire job ID for tracking background job status.
    /// </summary>
    [MaxLength(100)]
    public string? HangfireJobId { get; set; }

    // Navigation
    public Hotel Hotel { get; set; } = null!;
}
