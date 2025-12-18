using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TubeTracker.API.Attributes;
using TubeTracker.API.Extensions;
using TubeTracker.API.Models.Entities;
using TubeTracker.API.Repositories;

namespace TubeTracker.API.Controllers.Tracking.Stations;

[ApiController]
[Route("api/tracking/stations")]
[Tags("Tracking")]
[RequireVerifiedAccount]
public class DeleteTrackedStationController(ITrackedStationRepository trackedStationRepository, ILogger<DeleteTrackedStationController> logger) : ControllerBase
{
    [HttpDelete("{stationId:int}")]
    [Authorize]
    public async Task<IActionResult> Handle(int stationId)
    {
        int? userId = User.GetUserId();
        if (userId is null)
        {
            logger.LogWarning("Unauthorized attempt to delete tracked station - missing userId.");
            return Unauthorized("Token does not contain a sub claim.");
        }

        TrackedStation? existingTrackedStation = await trackedStationRepository.GetAsync(userId.Value, stationId);
        if (existingTrackedStation is null)
        {
            logger.LogInformation("User {UserId} attempted to unsubscribe from station {StationId} but was not subscribed.", userId, stationId);
            return BadRequest(new { message = "You are not subscribed to this station." });
        }

        await trackedStationRepository.DeleteAsync(userId.Value, stationId);

        logger.LogInformation("User {UserId} successfully unsubscribed from station {StationId}.", userId, stationId);

        return Ok(new { message = "Station subscription removed successfully." });
    }
}
