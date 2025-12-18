using TubeTracker.API.Repositories;
using TubeTracker.API.Settings;

namespace TubeTracker.API.Services.Background;

public class HistoryCleanupBackgroundService(
    IServiceScopeFactory serviceScopeFactory,
    TimeProvider timeProvider,
    StatusBackgroundSettings settings,
    ILogger<HistoryCleanupBackgroundService> logger) : BackgroundService
{
    private readonly TimeSpan _period = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(_period, timeProvider);

        do
        {
            await CleanupHistoryAsync();
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task CleanupHistoryAsync()
    {
        try
        {
            logger.LogInformation("Starting history cleanup...");
            using IServiceScope scope = serviceScopeFactory.CreateScope();
            ILineStatusHistoryRepository lineHistoryRepository = scope.ServiceProvider.GetRequiredService<ILineStatusHistoryRepository>();
            IStationStatusHistoryRepository stationHistoryRepository = scope.ServiceProvider.GetRequiredService<IStationStatusHistoryRepository>();

            DateTime threshold = timeProvider.GetUtcNow().UtcDateTime.AddDays(-settings.HistoryCleanupDays);
            
            int linesDeleted = await lineHistoryRepository.DeleteOldHistoryAsync(threshold);
            int stationsDeleted = await stationHistoryRepository.DeleteOldHistoryAsync(threshold);

            logger.LogInformation("History cleanup completed. Deleted {LineCount} line history records and {StationCount} station history records older than {Threshold}.", linesDeleted, stationsDeleted, threshold);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred during history cleanup.");
        }
    }
}
