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
public class UpdateTrackedLineController(ITrackedLineRepository trackedLineRepository, ILogger<UpdateTrackedLineController> logger) : ControllerBase
{
    [HttpPut]
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
            logger.LogWarning("Unauthorized attempt to update tracked line - missing userId.");
            return Unauthorized("Token does not contain a sub claim.");
        }

        TrackedLine? trackedLine = await trackedLineRepository.GetAsync(userId.Value, request.LineId);
        if (trackedLine is null)
        {
            logger.LogInformation("User {UserId} attempted to update subscription for line {LineId} but was not subscribed.", userId, request.LineId);
            return NotFound(new { message = "You are not subscribed to this line." });
        }

        trackedLine.Notify = request.Notify;
        trackedLine.MinUrgency = request.MinUrgency;
        await trackedLineRepository.UpdateAsync(trackedLine);

        logger.LogInformation("User {UserId} updated subscription for line {LineId}.", userId, request.LineId);

        return Ok(new { message = "Line subscription updated successfully." });
    }
}
