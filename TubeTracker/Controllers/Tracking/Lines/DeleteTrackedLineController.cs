using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TubeTracker.API.Attributes;
using TubeTracker.API.Extensions;
using TubeTracker.API.Models.Entities;
using TubeTracker.API.Repositories;

namespace TubeTracker.API.Controllers.Tracking.Lines;

[ApiController]
[Route("api/tracking/lines")]
[Tags("Tracking")]
[RequireVerifiedAccount]
public class DeleteTrackedLineController(ITrackedLineRepository trackedLineRepository, ILogger<DeleteTrackedLineController> logger) : ControllerBase
{
    [HttpDelete("{lineId:int}")]
    [Authorize]
    public async Task<IActionResult> Handle(int lineId)
    {
        int? userId = User.GetUserId();
        if (userId is null)
        {
            logger.LogWarning("Unauthorized attempt to delete tracked line - missing userId.");
            return Unauthorized("Token does not contain a sub claim.");
        }

        TrackedLine? existingTrackedLine = await trackedLineRepository.GetAsync(userId.Value, lineId);
        if (existingTrackedLine is null)
        {
            logger.LogInformation("User {UserId} attempted to unsubscribe from line {LineId} but was not subscribed.", userId, lineId);
            return BadRequest(new { message = "You are not subscribed to this line." });
        }

        await trackedLineRepository.DeleteAsync(userId.Value, lineId);

        logger.LogInformation("User {UserId} successfully unsubscribed from line {LineId}.", userId, lineId);

        return Ok(new { message = "Line subscription removed successfully." });
    }
}
