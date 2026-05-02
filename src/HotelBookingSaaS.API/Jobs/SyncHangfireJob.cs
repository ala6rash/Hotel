using Hangfire;
using HotelBookingSaaS.API.Services;
using Microsoft.Extensions.Logging;

namespace HotelBookingSaaS.API.Jobs;

/// <summary>
/// Hangfire recurring job: pulls bookings from all PMS + OTA sources once per hour.
/// Scheduled in Program.cs via RecurringJob.AddOrUpdate.
/// </summary>
public class SyncHangfireJob
{
    private readonly IBookingSyncService _syncService;
    private readonly ILogger<SyncHangfireJob> _logger;

    public SyncHangfireJob(IBookingSyncService syncService, ILogger<SyncHangfireJob> logger)
    {
        _syncService = syncService;
        _logger = logger;
    }

    /// <summary>Triggered every hour by Hangfire scheduler.</summary>
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 60, 300, 600 })]
    public async Task RunHourlySync()
    {
        _logger.LogInformation("Hourly sync started at {Time}", DateTime.UtcNow);
        try
        {
            await _syncService.SyncAllHotelsAsync();
            _logger.LogInformation("Hourly sync completed at {Time}", DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Hourly sync failed");
            throw; // Let Hangfire handle retry
        }
    }

    /// <summary>On-demand sync for a single hotel (enqueued via API).</summary>
    [AutomaticRetry(Attempts = 2)]
    public async Task RunHotelSync(int hotelId)
    {
        _logger.LogInformation("On-demand sync for hotel {HotelId}", hotelId);
        await _syncService.SyncHotelAsync(hotelId);
    }
}
