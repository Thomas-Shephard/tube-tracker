using TubeTracker.API.Models.Entities;
using TubeTracker.API.Models.Tfl;
using TubeTracker.API.Models.Classification;
using TubeTracker.API.Repositories;
using TubeTracker.API.Settings;

namespace TubeTracker.API.Services.Background;

public class TubeStatusBackgroundService(
    IServiceScopeFactory serviceScopeFactory,
    TimeProvider timeProvider,
    StatusBackgroundSettings settings,
    ILogger<TubeStatusBackgroundService> logger) : BackgroundService
{
    private readonly TimeSpan _period = TimeSpan.FromMinutes(settings.RefreshIntervalMinutes);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Give OllamaModelInitializer time to pull the model if needed
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        using PeriodicTimer timer = new(_period, timeProvider);

        do
        {
            await FetchAndStoreStatusesAsync();
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task FetchAndStoreStatusesAsync()
    {
        try
        {
            logger.LogInformation("Fetching tube statuses...");
            using IServiceScope scope = serviceScopeFactory.CreateScope();
            ITflService tflService = scope.ServiceProvider.GetRequiredService<ITflService>();
            ILineRepository lineRepository = scope.ServiceProvider.GetRequiredService<ILineRepository>();
            ILineStatusHistoryRepository lineHistoryRepository = scope.ServiceProvider.GetRequiredService<ILineStatusHistoryRepository>();
            IStationRepository stationRepository = scope.ServiceProvider.GetRequiredService<IStationRepository>();
            IStationStatusHistoryRepository stationHistoryRepository = scope.ServiceProvider.GetRequiredService<IStationStatusHistoryRepository>();
            ITflClassificationService classificationService = scope.ServiceProvider.GetRequiredService<ITflClassificationService>();
            IStationStatusSeverityRepository severityRepository = scope.ServiceProvider.GetRequiredService<IStationStatusSeverityRepository>();

            // Pre-fetch severity IDs
            IEnumerable<StationStatusSeverity> severities = await severityRepository.GetAllAsync();
            Dictionary<string, int> severityMap = severities.ToDictionary(s => s.Description, s => s.SeverityId, StringComparer.OrdinalIgnoreCase);

            if (!severityMap.TryGetValue("Good Service", out int goodServiceId))
            {
                throw new InvalidOperationException("Critical configuration missing: 'Good Service' severity category not found in database.");
            }

            List<TflLine> tflLines = await tflService.GetLineStatusesAsync();
            if (tflLines.Count == 0)
            {
                logger.LogWarning("No line statuses fetched from TFL. Skipping line update to prevent data loss.");
            }
            else
            {
                logger.LogDebug("Fetched {Count} lines from TFL", tflLines.Count);
                IEnumerable<Line> dbLines = await lineRepository.GetAllAsync();
                Dictionary<string, int> lineMap = dbLines.ToDictionary(l => l.TflId, l => l.LineId);

                DateTime? lastLineReport = await lineHistoryRepository.GetLastReportTimeAsync();
                DateTime lineThreshold = lastLineReport.HasValue && (DateTime.UtcNow - lastLineReport.Value).TotalMinutes < 30
                    ? lastLineReport.Value.AddSeconds(-30)
                    : DateTime.UtcNow.AddMinutes(-settings.DeduplicationThresholdMinutes);

                foreach (TflLine tflLine in tflLines)
                {
                    if (!lineMap.TryGetValue(tflLine.Id, out int lineId))
                    {
                        logger.LogWarning("Line with TflId {TflId} not found in database. Skipping.", tflLine.Id);
                        continue;
                    }

                    foreach (LineStatus status in tflLine.LineStatuses)
                    {
                        await lineHistoryRepository.UpsertAsync(lineId, status.StatusSeverity, status.Reason, lineThreshold);
                    }
                }
            }

            List<TflStationDisruption> stationDisruptions = await tflService.GetStationDisruptionsAsync();

            logger.LogDebug("Fetched {Count} station disruptions from TFL", stationDisruptions.Count);
            DateTime? lastStationReport = await stationHistoryRepository.GetLastReportTimeAsync();
            DateTime stationThreshold = lastStationReport.HasValue && (DateTime.UtcNow - lastStationReport.Value).TotalMinutes < 30
                ? lastStationReport.Value.AddSeconds(-30)
                : DateTime.UtcNow.AddMinutes(-settings.DeduplicationThresholdMinutes);

            IEnumerable<Station> dbStations = (await stationRepository.GetAllAsync()).ToList();
            if (dbStations.Any())
            {
                Dictionary<string, int> stationMap = dbStations.ToDictionary(station => station.TflId, station => station.StationId);

                // Group disruptions by station ID to handle multiple disruptions and identify cleared stations
                Dictionary<int, List<string>> disruptionsByStation = new();
                foreach (TflStationDisruption disruption in stationDisruptions)
                {
                    string tflId = !string.IsNullOrEmpty(disruption.StationAtcoCode)
                        ? disruption.StationAtcoCode
                        : disruption.AtcoCode;

                    if (!string.IsNullOrEmpty(tflId) && stationMap.TryGetValue(tflId, out int stationId))
                    {
                        if (!disruptionsByStation.TryGetValue(stationId, out List<string>? descriptions))
                        {
                            descriptions = [];
                            disruptionsByStation[stationId] = descriptions;
                        }
                        descriptions.Add(disruption.Description);
                    }
                    else
                    {
                        logger.LogWarning("Station disruption for {TflId} ({CommonName}) not found in database.", tflId, disruption.CommonName);
                    }
                }

                foreach (Station station in dbStations)
                {
                    if (disruptionsByStation.TryGetValue(station.StationId, out List<string>? descriptions))
                    {
                        foreach (string description in descriptions)
                        {
                            int? historyId = await stationHistoryRepository.TryGetActiveHistoryIdAsync(station.StationId, description, stationThreshold);
                            if (historyId.HasValue)
                            {
                                await stationHistoryRepository.UpdateLastReportedAsync(historyId.Value);
                            }
                            else
                            {
                                StationClassificationResult classification = await classificationService.ClassifyStationDisruptionAsync(description);
                                await stationHistoryRepository.InsertAsync(station.StationId, description, classification.CategoryId, classification.IsFuture);
                            }
                        }
                    }
                    else
                    {
                        // No active disruptions, so record "No Issues"
                        const string description = "No Issues";
                        int? historyId = await stationHistoryRepository.TryGetActiveHistoryIdAsync(station.StationId, description, stationThreshold);
                        if (historyId.HasValue)
                        {
                            await stationHistoryRepository.UpdateLastReportedAsync(historyId.Value);
                        }
                        else
                        {
                            await stationHistoryRepository.InsertAsync(station.StationId, description, goodServiceId, false);
                        }
                    }
                }
            }
            logger.LogInformation("Tube status update completed.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to update tube statuses.");
        }
    }
}