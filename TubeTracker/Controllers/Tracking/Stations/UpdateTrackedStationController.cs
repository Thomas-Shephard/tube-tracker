using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TubeTracker.API.Attributes;
using TubeTracker.API.Extensions;
using TubeTracker.API.Models.Entities;
using TubeTracker.API.Models.Requests;
using TubeTracker.API.Repositories;

namespace TubeTracker.API.Controllers.Tracking.Stations;

[ApiController]
[Route("api/tracking/stations")]
[Tags("Tracking/Stations")]
[RequireVerifiedAccount]
public class UpdateTrackedStationController(ITrackedStationRepository trackedStationRepository, ILogger<UpdateTrackedStationController> logger) : ControllerBase
{
    [HttpPut]
    [Authorize]
    public async Task<IActionResult> Handle([FromBody] TrackedStationRequestModel request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        int? userId = User.GetUserId();
        if (userId is null)
        {
            logger.LogWarning("Unauthorized attempt to update tracked station - missing userId.");
            return Unauthorized("Token does not contain a sub claim.");
        }

        TrackedStation? trackedStation = await trackedStationRepository.GetAsync(userId.Value, request.StationId);
        if (trackedStation is null)
        {
            logger.LogInformation("User {UserId} attempted to update subscription for station {StationId} but was not subscribed.", userId, request.StationId);
            return NotFound(new { message = "You are not subscribed to this station." });
        }

        trackedStation.Notify = request.Notify;
        trackedStation.MinUrgency = request.MinUrgency;
        await trackedStationRepository.UpdateAsync(trackedStation);

        logger.LogInformation("User {UserId} updated subscription for station {StationId}.", userId, request.StationId);

        return Ok(new { message = "Station subscription updated successfully." });
    }
}
