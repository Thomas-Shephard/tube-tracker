using TubeTracker.API.Models.Classification;
using TubeTracker.API.Models.Entities;
using TubeTracker.API.Repositories;

namespace TubeTracker.API.Services.Background;

public class StationClassificationBackgroundService(
    IServiceScopeFactory serviceScopeFactory,
    TimeProvider timeProvider,
    ILogger<StationClassificationBackgroundService> logger) : BackgroundService
{
    private readonly TimeSpan _delay = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessPendingClassificationsAsync(stoppingToken);
            
            try
            {
                await Task.Delay(_delay, timeProvider, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ProcessPendingClassificationsAsync(CancellationToken stoppingToken)
    {
        try
        {
            using IServiceScope scope = serviceScopeFactory.CreateScope();
            IStationStatusHistoryRepository historyRepository = scope.ServiceProvider.GetRequiredService<IStationStatusHistoryRepository>();
            IStationStatusSeverityRepository severityRepository = scope.ServiceProvider.GetRequiredService<IStationStatusSeverityRepository>();
            ITflClassificationService classificationService = scope.ServiceProvider.GetRequiredService<ITflClassificationService>();

            IEnumerable<StationStatusSeverity> severities = await severityRepository.GetAllAsync();
            StationStatusSeverity? pendingSeverity = severities.FirstOrDefault(s => s.Description == "Pending Classification");

            if (pendingSeverity == null)
            {
                logger.LogWarning("'Pending Classification' severity category not found in database. Skipping classification pass.");
                return;
            }

            IEnumerable<string> pendingDescriptions = await historyRepository.GetDistinctDescriptionsBySeverityAsync(pendingSeverity.SeverityId);
            List<string> descriptionsToProcess = pendingDescriptions.ToList();

            if (descriptionsToProcess.Count == 0)
            {
                return;
            }

            logger.LogInformation("Processing {Count} unique pending station disruption descriptions...", descriptionsToProcess.Count);

            foreach (string description in descriptionsToProcess)
            {
                if (stoppingToken.IsCancellationRequested) break;

                try
                {
                    StationClassificationResult result = await classificationService.ClassifyStationDisruptionAsync(description);
                    await historyRepository.UpdateClassificationByDescriptionAsync(description, pendingSeverity.SeverityId, result.CategoryId, result.IsFuture);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to classify description: {Description}", description);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in StationClassificationBackgroundService.");
        }
    }
}
