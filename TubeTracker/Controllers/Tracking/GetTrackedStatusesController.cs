using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TubeTracker.API.Extensions;
using TubeTracker.API.Models.Entities;
using TubeTracker.API.Repositories;

namespace TubeTracker.API.Controllers.Tracking;

[ApiController]
[Route("api/status/tracked")]
[Authorize]
[Tags("Status")]
public class GetTrackedStatusesController(
    ILineRepository lineRepository,
    IStationRepository stationRepository,
    ILineStatusHistoryRepository lineStatusHistoryRepository,
    IStationStatusHistoryRepository stationStatusHistoryRepository,
    ITrackedLineRepository trackedLineRepository,
    ITrackedStationRepository trackedStationRepository) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetTrackedStatuses()
    {
        int? userId = User.GetUserId();
        if (userId == null) return Unauthorized();

        IEnumerable<TrackedLine> trackedLines = await trackedLineRepository.GetByUserIdAsync(userId.Value);
        IEnumerable<TrackedStation> trackedStations = await trackedStationRepository.GetByUserIdAsync(userId.Value);

        List<object> lineResults = [];
        foreach (TrackedLine trackedLine in trackedLines)
        {
            Line? line = await lineRepository.GetByIdAsync(trackedLine.LineId);
            if (line is null) continue;
            
            IEnumerable<LineStatusHistory> statuses = await lineStatusHistoryRepository.GetActiveByLineIdAsync(trackedLine.LineId);
            lineResults.Add(new
            {
                line.LineId,
                line.Name,
                line.TflId,
                trackedLine.Notify,
                trackedLine.MinUrgency,
                Statuses = statuses
            });
        }

        List<object> stationResults = [];
        foreach (TrackedStation trackedStation in trackedStations)
        {
            Station? station = await stationRepository.GetByIdAsync(trackedStation.StationId);
            if (station == null) continue;

            IEnumerable<StationStatusHistory> statuses = await stationStatusHistoryRepository.GetActiveByStationIdAsync(trackedStation.StationId);
            stationResults.Add(new
            {
                station.StationId,
                station.CommonName,
                station.TflId,
                trackedStation.Notify,
                Statuses = statuses
            });
        }

        return Ok(new
        {
            Lines = lineResults,
            Stations = stationResults
        });
    }
}
