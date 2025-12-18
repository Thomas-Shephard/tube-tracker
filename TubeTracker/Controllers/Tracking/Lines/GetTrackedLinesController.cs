using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TubeTracker.API.Extensions;
using TubeTracker.API.Models.Entities;
using TubeTracker.API.Repositories;

namespace TubeTracker.API.Controllers.Tracking.Lines;

[ApiController]
[Route("api/tracking/lines")]
[Tags("Tracking")]
public class GetTrackedLinesController(ITrackedLineRepository trackedLineRepository, ILogger<GetTrackedLinesController> logger) : ControllerBase
{
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Handle()
    {
        int? userId = User.GetUserId();
        if (userId is null)
        {
            logger.LogWarning("Unauthorized attempt to get tracked lines - missing userId.");
            return Unauthorized("Token does not contain a sub claim.");
        }

        IEnumerable<TrackedLine> trackedLines = await trackedLineRepository.GetByUserIdAsync(userId.Value);
        
        logger.LogInformation("User {UserId} fetched their tracked lines.", userId);
        
        return Ok(trackedLines);
    }
}
