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

            if (!severityMap.TryGetValue("No Disruptions", out int goodServiceId))
            {
                throw new InvalidOperationException("Critical configuration missing: 'No Disruptions' severity category not found in database.");
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
                Dictionary<int, List<string>> disruptionsByStation = new();

                foreach (TflStationDisruption disruption in stationDisruptions)
                {
                    string tflId = !string.IsNullOrEmpty(disruption.StationAtcoCode) ? disruption.StationAtcoCode : disruption.AtcoCode;
                    if (!string.IsNullOrEmpty(tflId) && stationMap.TryGetValue(tflId, out int stationId))
                    {
                        if (!disruptionsByStation.TryGetValue(stationId, out List<string>? descriptions))
                        {
                            descriptions = [];
                            disruptionsByStation[stationId] = descriptions;
                        }
                        descriptions.Add(disruption.Description);
                    }
                }

                List<(int StationId, string Description)> newDisruptionsToClassify = [];

                foreach (Station station in dbStations)
                {
                    if (disruptionsByStation.TryGetValue(station.StationId, out List<string>? descriptions))
                    {
                        foreach (string description in descriptions)
                        {
                            StationStatusHistory? existing = await stationHistoryRepository.GetActiveHistoryAsync(station.StationId, description, stationThreshold);
                            if (existing is not null)
                            {
                                await stationHistoryRepository.UpdateLastReportedAsync(existing.HistoryId);
                            }
                            else
                            {
                                newDisruptionsToClassify.Add((station.StationId, description));
                            }
                        }
                    }
                    else
                    {
                        // No Issues
                        const string description = "No Disruptions";
                        StationStatusHistory? existing = await stationHistoryRepository.GetActiveHistoryAsync(station.StationId, description, stationThreshold);
                        if (existing is not null)
                        {
                            await stationHistoryRepository.UpdateLastReportedAsync(existing.HistoryId);
                        }
                        else
                        {
                            await stationHistoryRepository.InsertAsync(station.StationId, description, goodServiceId, false);
                        }
                    }
                }

                // Batch Classification
                if (newDisruptionsToClassify.Count > 0)
                {
                    logger.LogInformation("Classifying {Count} new or re-evaluating station disruptions...", newDisruptionsToClassify.Count);
                    
                    // Deduplicate strings to avoid redundant LLM calls
                    List<string> uniqueDescriptions = newDisruptionsToClassify.Select(x => x.Description).Distinct().ToList();
                    
                    // Group the pending disruptions by description for efficient lookup
                    var pendingByDescription = newDisruptionsToClassify
                        .GroupBy(x => x.Description)
                        .ToDictionary(g => g.Key, g => g.Select(x => x.StationId).ToList());

                    foreach (string desc in uniqueDescriptions)
                    {
                        // Classify one unique description
                        StationClassificationResult result = await classificationService.ClassifyStationDisruptionAsync(desc);

                        // Immediately update ALL stations that have this description
                        if (pendingByDescription.TryGetValue(desc, out List<int>? stationIds))
                        {
                            foreach (int stationId in stationIds)
                            {
                                await stationHistoryRepository.InsertAsync(stationId, desc, result.CategoryId, result.IsFuture);
                            }
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
