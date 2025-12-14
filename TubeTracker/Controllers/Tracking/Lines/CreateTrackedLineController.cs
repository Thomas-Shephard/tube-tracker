using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TubeTracker.API.Extensions;
using TubeTracker.API.Models.Entities;
using TubeTracker.API.Models.Requests;
using TubeTracker.API.Repositories;

namespace TubeTracker.API.Controllers.Tracking.Lines;

[ApiController]
[Route("api/tracking/lines")]
[Tags("Tracking")]
public class CreateTrackedLineController(ITrackedLineRepository trackedLineRepository) : ControllerBase
{
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Handle([FromBody] TrackedLineRequestModel request)
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

        TrackedLine? existingTrackedLine = await trackedLineRepository.GetAsync(userId.Value, request.LineId);
        if (existingTrackedLine is not null)
        {
            return Conflict(new { message = "You are already subscribed to this line." });
        }

        TrackedLine trackedLine = new()
        {
            UserId = userId.Value,
            LineId = request.LineId,
            Notify = request.Notify,
            MinUrgency = request.MinUrgency,
            CreatedAt = DateTime.UtcNow
        };
        await trackedLineRepository.AddAsync(trackedLine);

        return Ok(new { message = "Line subscription created successfully." });
    }
}
