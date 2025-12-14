using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TubeTracker.API.Extensions;
using TubeTracker.API.Models.Entities;
using TubeTracker.API.Models.Requests;
using TubeTracker.API.Repositories;

namespace TubeTracker.API.Controllers.Tracking.Stations;

[ApiController]
[Route("api/tracking/stations")]
[Tags("Tracking")]
public class CreateTrackedStationController(ITrackedStationRepository trackedStationRepository) : ControllerBase
{
    [HttpPost]
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
            return Unauthorized("Token does not contain a sub claim.");
        }

        TrackedStation? existingTrackedStation = await trackedStationRepository.GetAsync(userId.Value, request.StationId);
        if (existingTrackedStation is not null)
        {
            return Conflict(new { message = "You are already subscribed to this station." });
        }

        TrackedStation trackedStation = new()
        {
            UserId = userId.Value,
            StationId = request.StationId,
            Notify = request.Notify,
            NotifyAccessibility = request.NotifyAccessibility,
            MinUrgency = request.MinUrgency,
            CreatedAt = DateTime.UtcNow
        };
        await trackedStationRepository.AddAsync(trackedStation);

        return Ok(new { message = "Station subscription created successfully." });
    }
}
