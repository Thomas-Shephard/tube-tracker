using TubeTracker.API.Models.Entities;
using TubeTracker.API.Models.Tfl;
using TubeTracker.API.Repositories;
using TubeTracker.API.Settings;

namespace TubeTracker.API.Services.Background;

public class TubeStatusBackgroundService(IServiceScopeFactory serviceScopeFactory, TimeProvider timeProvider, StatusBackgroundSettings settings) : BackgroundService
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
            using IServiceScope scope = serviceScopeFactory.CreateScope();
            ITflService tflService = scope.ServiceProvider.GetRequiredService<ITflService>();
            ILineRepository lineRepository = scope.ServiceProvider.GetRequiredService<ILineRepository>();
            ILineStatusHistoryRepository lineHistoryRepository = scope.ServiceProvider.GetRequiredService<ILineStatusHistoryRepository>();
            IStationRepository stationRepository = scope.ServiceProvider.GetRequiredService<IStationRepository>();
            IStationStatusHistoryRepository stationHistoryRepository = scope.ServiceProvider.GetRequiredService<IStationStatusHistoryRepository>();

            List<TflLine> tflLines = await tflService.GetLineStatusesAsync();
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
                    Console.WriteLine($"Line with TflId {tflLine.Id} not found in database. Skipping.");
                    continue;
                }

                foreach (LineStatus status in tflLine.LineStatuses)
                {
                    await lineHistoryRepository.UpsertAsync(lineId, status.StatusSeverity, lineThreshold);
                }
            }

            List<TflStationDisruption> stationDisruptions = await tflService.GetStationDisruptionsAsync();
            DateTime? lastStationReport = await stationHistoryRepository.GetLastReportTimeAsync();
            DateTime stationThreshold = lastStationReport.HasValue && (DateTime.UtcNow - lastStationReport.Value).TotalMinutes < 30
                ? lastStationReport.Value.AddSeconds(-30)
                : DateTime.UtcNow.AddMinutes(-settings.DeduplicationThresholdMinutes);

            if (stationDisruptions.Count != 0)
            {
                IEnumerable<Station> dbStations = await stationRepository.GetAllAsync();
                Dictionary<string, int> stationMap = dbStations.ToDictionary(station => station.TflId, station => station.StationId);

                foreach (TflStationDisruption disruption in stationDisruptions)
                {
                    string tflId = string.IsNullOrEmpty(disruption.StationAtcoCode)
                        ? disruption.StationAtcoCode
                        : disruption.AtcoCode;

                    if (!string.IsNullOrEmpty(tflId) && stationMap.TryGetValue(tflId, out int stationId))
                    {
                        await stationHistoryRepository.UpsertAsync(stationId, disruption.Description, stationThreshold);
                    }
                    else
                    {
                         Console.WriteLine($"Station disruption for {tflId} ({disruption.CommonName}) not found in database.");
                    }
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to update tube statuses: {e.Message}");
        }
    }
}
