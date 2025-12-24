using TubeTracker.API.Models.Entities;
using TubeTracker.API.Models.Tfl;
using TubeTracker.API.Repositories;
using TubeTracker.API.Settings;

namespace TubeTracker.API.Services.Background;

public class TubeStatusBackgroundService(
    IServiceScopeFactory serviceScopeFactory,
    TimeProvider timeProvider,
    StatusBackgroundSettings settings,
    ILogger<TubeStatusBackgroundService> logger) : BackgroundService
{
    private readonly TimeSpan _delay = TimeSpan.FromMinutes(settings.RefreshIntervalMinutes);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await FetchAndStoreStatusesAsync();
            
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
            IStationStatusSeverityRepository severityRepository = scope.ServiceProvider.GetRequiredService<IStationStatusSeverityRepository>();

            // Fetch severity IDs
            IEnumerable<StationStatusSeverity> severities = (await severityRepository.GetAllAsync()).ToList();
            Dictionary<string, int> severityMap = severities.ToDictionary(s => s.Description, s => s.SeverityId, StringComparer.OrdinalIgnoreCase);

            if (!severityMap.TryGetValue("No Disruptions", out int goodServiceId))
            {
                throw new InvalidOperationException("Critical configuration missing: 'No Disruptions' severity category not found in database.");
            }
            
            if (!severityMap.TryGetValue("Pending Classification", out int pendingId))
            {
                logger.LogWarning("'Pending Classification' severity category not found. Falling back to 'Other'.");
                if (!severityMap.TryGetValue("Other", out pendingId))
                {
                    throw new InvalidOperationException("Critical configuration missing: Neither 'Pending Classification' nor 'Other' severity category found.");
                }
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
                DateTime lineThreshold = lastLineReport.HasValue && (DateTime.UtcNow - lastLineReport.Value).TotalMinutes < 60
                    ? lastLineReport.Value.AddMinutes(-settings.RefreshIntervalMinutes * 2)
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
            DateTime stationThreshold = lastStationReport.HasValue && (DateTime.UtcNow - lastStationReport.Value).TotalMinutes < 60
                ? lastStationReport.Value.AddMinutes(-settings.RefreshIntervalMinutes * 2)
                : DateTime.UtcNow.AddMinutes(-settings.DeduplicationThresholdMinutes);

            IEnumerable<Station> dbStations = (await stationRepository.GetAllAsync()).ToList();
            if (dbStations.Any())
            {
                Dictionary<string, int> stationMap = dbStations.ToDictionary(station => station.TflId, station => station.StationId);
                Dictionary<int, HashSet<string>> disruptionsByStation = new();

                foreach (TflStationDisruption disruption in stationDisruptions)
                {
                    string tflId = !string.IsNullOrEmpty(disruption.StationAtcoCode) ? disruption.StationAtcoCode : disruption.AtcoCode;
                    if (!string.IsNullOrEmpty(tflId) && stationMap.TryGetValue(tflId, out int stationId))
                    {
                        if (!disruptionsByStation.TryGetValue(stationId, out HashSet<string>? descriptions))
                        {
                            descriptions = [];
                            disruptionsByStation[stationId] = descriptions;
                        }
                        
                        string cleanDescription = disruption.Description.Trim();
                        if (!string.IsNullOrEmpty(cleanDescription))
                        {
                            descriptions.Add(cleanDescription);
                        }
                    }
                }

                // Batch fetch active history to improve efficiency
                IEnumerable<StationStatusHistory> activeHistories = await stationHistoryRepository.GetAllActiveHistoryAsync(stationThreshold);
                var activeHistoryLookup = activeHistories
                    .GroupBy(h => (h.StationId, h.StatusDescription))
                    .ToDictionary(g => g.Key, g => g.First());

                foreach (Station station in dbStations)
                {
                    if (disruptionsByStation.TryGetValue(station.StationId, out HashSet<string>? descriptions))
                    {
                        foreach (string description in descriptions)
                        {
                            if (activeHistoryLookup.TryGetValue((station.StationId, description), out StationStatusHistory? existing))
                            {
                                await stationHistoryRepository.UpdateLastReportedAsync(existing.HistoryId);
                            }
                            else
                            {
                                await stationHistoryRepository.InsertAsync(station.StationId, description, pendingId, false);
                            }
                        }
                    }
                    else
                    {
                        // No Issues
                        const string description = "No Disruptions";
                        if (activeHistoryLookup.TryGetValue((station.StationId, description), out StationStatusHistory? existing))
                        {
                            await stationHistoryRepository.UpdateLastReportedAsync(existing.HistoryId);
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
