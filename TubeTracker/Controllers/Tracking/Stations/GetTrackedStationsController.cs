using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TubeTracker.API.Extensions;
using TubeTracker.API.Models.Entities;
using TubeTracker.API.Repositories;

namespace TubeTracker.API.Controllers.Tracking.Stations;

[ApiController]
[Route("api/tracking/stations")]
[Tags("Tracking")]
public class GetTrackedStationsController(ITrackedStationRepository trackedStationRepository, ILogger<GetTrackedStationsController> logger) : ControllerBase
{
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Handle()
    {
        int? userId = User.GetUserId();
        if (userId is null)
        {
            logger.LogWarning("Unauthorized attempt to get tracked stations - missing userId.");
            return Unauthorized("Token does not contain a sub claim.");
        }

        IEnumerable<TrackedStation> trackedStations = await trackedStationRepository.GetByUserIdAsync(userId.Value);

        logger.LogInformation("User {UserId} fetched their tracked stations.", userId);

        return Ok(trackedStations);
    }
}
