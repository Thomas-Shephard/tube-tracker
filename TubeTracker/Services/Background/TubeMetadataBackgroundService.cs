using TubeTracker.API.Models.Entities;
using TubeTracker.API.Models.Tfl;
using TubeTracker.API.Repositories;

namespace TubeTracker.API.Services.Background;

public class TubeMetadataBackgroundService(IServiceScopeFactory serviceScopeFactory, TimeProvider timeProvider, ILogger<TubeMetadataBackgroundService> logger) : BackgroundService
{
    private readonly TimeSpan _period = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(_period, timeProvider);

        do
        {
            await UpdateMetadataAsync();
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task UpdateMetadataAsync()
    {
        logger.LogInformation("Starting metadata update...");
        using IServiceScope scope = serviceScopeFactory.CreateScope();
        ITflService tflService = scope.ServiceProvider.GetRequiredService<ITflService>();
        ILineRepository lineRepository = scope.ServiceProvider.GetRequiredService<ILineRepository>();
        IStationRepository stationRepository = scope.ServiceProvider.GetRequiredService<IStationRepository>();

        try
        {
            List<TflLine> tflLines = await tflService.GetLineStatusesAsync();
            int linesUpdated = 0;
            int linesAdded = 0;

            foreach (TflLine tflLine in tflLines)
            {
                Line? existingLine = await lineRepository.GetByTflIdAsync(tflLine.Id);

                if (existingLine is null)
                {
                    Line newLine = new()
                    {
                        TflId = tflLine.Id,
                        Name = tflLine.Name,
                        ModeName = tflLine.ModeName,
                        Colour = null
                    };
                    await lineRepository.AddAsync(newLine);
                    linesAdded++;
                }
                else
                {
                    bool changed = existingLine.Name != tflLine.Name || existingLine.ModeName != tflLine.ModeName;
                    if (!changed) continue;
                    existingLine.Name = tflLine.Name;
                    existingLine.ModeName = tflLine.ModeName;
                    await lineRepository.UpdateAsync(existingLine);
                    linesUpdated++;
                }
            }

            List<TflStopPoint> tflStations = await tflService.GetStationsAsync();
            int stationsUpdated = 0;
            int stationsAdded = 0;

            string[] allowedStopTypes = ["NaptanMetroStation", "NaptanRailStation", "NaptanTrainStation", "NaptanDlrStation"];

            IEnumerable<TflStopPoint> uniqueStations = tflStations
                                                       .Where(s => allowedStopTypes.Contains(s.StopType))
                                                       .GroupBy(s => s.CommonName)
                                                       .Select(g => g.OrderBy(s => s.Id.StartsWith('9') ? 0 : 1).First());

            foreach (TflStopPoint stopPoint in uniqueStations)
            {
                Station? existingStation = await stationRepository.GetByTflIdAsync(stopPoint.Id) ?? await stationRepository.GetByCommonNameAsync(stopPoint.CommonName);

                if (existingStation is null)
                {
                    Station newStation = new()
                    {
                        TflId = stopPoint.Id,
                        CommonName = stopPoint.CommonName,
                        Lat = stopPoint.Lat,
                        Lon = stopPoint.Lon
                    };
                    await stationRepository.AddAsync(newStation);
                    stationsAdded++;
                }
                else
                {
                    bool changed = existingStation.TflId != stopPoint.Id ||
                                   existingStation.CommonName != stopPoint.CommonName ||
                                   Math.Abs((existingStation.Lat ?? 0) - stopPoint.Lat) > 0.000001 ||
                                   Math.Abs((existingStation.Lon ?? 0) - stopPoint.Lon) > 0.000001;

                    if (!changed) continue;
                    existingStation.TflId = stopPoint.Id;
                    existingStation.CommonName = stopPoint.CommonName;
                    existingStation.Lat = stopPoint.Lat;
                    existingStation.Lon = stopPoint.Lon;
                    await stationRepository.UpdateAsync(existingStation);
                    stationsUpdated++;
                }
            }
            logger.LogInformation("Metadata update finished. Lines: {Added} added, {Updated} updated. Stations: {StationsAdded} added, {StationsUpdated} updated.", linesAdded, linesUpdated, stationsAdded, stationsUpdated);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to update metadata.");
        }
    }
}
