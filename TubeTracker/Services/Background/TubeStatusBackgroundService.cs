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
                        await lineHistoryRepository.UpsertAsync(lineId, status.StatusSeverity, lineThreshold);
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
                            await stationHistoryRepository.UpsertAsync(station.StationId, description, stationThreshold);
                        }
                    }
                    else
                    {
                        // No active disruptions, so record "No Issues"
                        await stationHistoryRepository.UpsertAsync(station.StationId, "No Issues", stationThreshold);
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
