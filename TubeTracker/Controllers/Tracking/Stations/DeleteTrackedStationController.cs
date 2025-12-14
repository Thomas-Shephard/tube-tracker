using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TubeTracker.API.Extensions;
using TubeTracker.API.Models.Entities;
using TubeTracker.API.Repositories;

namespace TubeTracker.API.Controllers.Tracking.Stations;

[ApiController]
[Route("api/tracking/stations")]
[Tags("Tracking")]
public class DeleteTrackedStationController(ITrackedStationRepository trackedStationRepository) : ControllerBase
{
    [HttpDelete("{stationId:int}")]
    [Authorize]
    public async Task<IActionResult> Handle(int stationId)
    {
        int? userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized("Token does not contain a sub claim.");
        }

        TrackedStation? existingTrackedStation = await trackedStationRepository.GetAsync(userId.Value, stationId);
        if (existingTrackedStation is not null)
        {
            return BadRequest(new { message = "You are not subscribed to this station." });
        }

        await trackedStationRepository.DeleteAsync(userId.Value, stationId);
        return Ok(new { message = "Station subscription removed successfully." });
    }
}
