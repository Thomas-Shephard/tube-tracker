using TubeTracker.API.Models.Entities;
using TubeTracker.API.Models.Tfl;
using TubeTracker.API.Repositories;

namespace TubeTracker.API.Services.Background;

public class TubeMetadataBackgroundService(IServiceScopeFactory serviceScopeFactory, TimeProvider timeProvider) : BackgroundService
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
        using IServiceScope scope = serviceScopeFactory.CreateScope();
        ITflService tflService = scope.ServiceProvider.GetRequiredService<ITflService>();
        ILineRepository lineRepository = scope.ServiceProvider.GetRequiredService<ILineRepository>();
        IStationRepository stationRepository = scope.ServiceProvider.GetRequiredService<IStationRepository>();

        try
        {
            List<TflLine> tflLines = await tflService.GetLineStatusesAsync();

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
                }
                else
                {
                    bool changed = existingLine.Name != tflLine.Name || existingLine.ModeName != tflLine.ModeName;
                    if (!changed) continue;
                    existingLine.Name = tflLine.Name;
                    existingLine.ModeName = tflLine.ModeName;
                    await lineRepository.UpdateAsync(existingLine);
                }
            }

            List<TflStopPoint> tflStations = await tflService.GetStationsAsync();

            foreach (TflStopPoint stopPoint in tflStations)
            {
                Station? existingStation = await stationRepository.GetByTflIdAsync(stopPoint.Id);

                if (existingStation == null)
                {
                    Station newStation = new()
                    {
                        TflId = stopPoint.Id,
                        CommonName = stopPoint.CommonName,
                        Lat = stopPoint.Lat,
                        Lon = stopPoint.Lon
                    };
                    await stationRepository.AddAsync(newStation);
                }
                else
                {
                    bool changed = existingStation.CommonName != stopPoint.CommonName ||
                                   Math.Abs((existingStation.Lat ?? 0) - stopPoint.Lat) > 0.000001 ||
                                   Math.Abs((existingStation.Lon ?? 0) - stopPoint.Lon) > 0.000001;

                    if (!changed) continue;
                    existingStation.CommonName = stopPoint.CommonName;
                    existingStation.Lat = stopPoint.Lat;
                    existingStation.Lon = stopPoint.Lon;
                    await stationRepository.UpdateAsync(existingStation);
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to update metadata: {e.Message}");
        }
    }
}
