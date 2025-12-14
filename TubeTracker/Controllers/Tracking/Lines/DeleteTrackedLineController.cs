using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TubeTracker.API.Extensions;
using TubeTracker.API.Models.Entities;
using TubeTracker.API.Repositories;

namespace TubeTracker.API.Controllers.Tracking.Lines;

[ApiController]
[Route("api/tracking/lines")]
[Tags("Tracking")]
public class DeleteTrackedLineController(ITrackedLineRepository trackedLineRepository) : ControllerBase
{
    [HttpDelete("{lineId:int}")]
    [Authorize]
    public async Task<IActionResult> Handle(int lineId)
    {
        int? userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized("Token does not contain a sub claim.");
        }

        TrackedLine? existingTrackedLine = await trackedLineRepository.GetAsync(userId.Value, lineId);
        if (existingTrackedLine is null)
        {
            return BadRequest(new { message = "You are not subscribed to this line." });
        }

        await trackedLineRepository.DeleteAsync(userId.Value, lineId);
        return Ok(new { message = "Line subscription removed successfully." });
    }
}
