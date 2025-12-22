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
                                bool shouldReclassify = false;
                                DateTime now = DateTime.UtcNow;

                                // Case 1: Was future, but start time passed -> Re-check to see if active
                                if (existing is { IsFuture: true, ValidFrom: not null } && existing.ValidFrom.Value <= now)
                                {
                                    shouldReclassify = true;
                                }
                                // Case 2: Was active, but end time passed -> Re-check to see if future (next occurrence)
                                else if (existing is { IsFuture: false, ValidUntil: not null } && existing.ValidUntil.Value <= now)
                                {
                                    shouldReclassify = true;
                                }

                                if (shouldReclassify)
                                {
                                    newDisruptionsToClassify.Add((station.StationId, description));
                                    // Note: We don't update last_reported here because we want the new classification to take over as a new entry (or update).
                                    // Actually, if we classify and insert, it will create a NEW history entry. 
                                    // The old one will eventually fall off due to threshold.
                                    // Ideally, we might want to "expire" the old one, but letting it age out is fine.
                                }
                                else
                                {
                                    await stationHistoryRepository.UpdateLastReportedAsync(existing.HistoryId);
                                }
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
                        const string description = "No Issues";
                        StationStatusHistory? existing = await stationHistoryRepository.GetActiveHistoryAsync(station.StationId, description, stationThreshold);
                        if (existing is not null)
                        {
                            await stationHistoryRepository.UpdateLastReportedAsync(existing.HistoryId);
                        }
                        else
                        {
                            await stationHistoryRepository.InsertAsync(station.StationId, description, goodServiceId, false, null, null);
                        }
                    }
                }

                if (newDisruptionsToClassify.Count > 0)
                {
                    logger.LogInformation("Classifying {Count} new or re-evaluating station disruptions...", newDisruptionsToClassify.Count);
                    
                    // Deduplicate strings to avoid redundant LLM calls
                    List<string> uniqueDescriptions = newDisruptionsToClassify.Select(x => x.Description).Distinct().ToList();
                    Dictionary<string, List<int>> pendingByDescription = newDisruptionsToClassify
                                                                         .GroupBy(x => x.Description)
                                                                         .ToDictionary(g => g.Key, g => g.Select(x => x.StationId).ToList());

                    foreach (string desc in uniqueDescriptions)
                    {
                        // Classify one unique description
                        StationClassificationResult result = await classificationService.ClassifyStationDisruptionAsync(desc);

                        // Immediately update all stations with this description
                        if (!pendingByDescription.TryGetValue(desc, out List<int>? stationIds)) continue;
                        foreach (int stationId in stationIds)
                        {
                            await stationHistoryRepository.InsertAsync(stationId, desc, result.CategoryId, result.IsFuture, result.ValidFrom, result.ValidUntil);
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
