using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TubeTracker.API.Extensions;
using TubeTracker.API.Models.Entities;
using TubeTracker.API.Repositories;

namespace TubeTracker.API.Controllers.Tracking.Lines;

[ApiController]
[Route("api/tracking/lines")]
[Tags("Tracking")]
public class GetTrackedLinesController(ITrackedLineRepository trackedLineRepository) : ControllerBase
{
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Handle()
    {
        int? userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized("Token does not contain a sub claim.");
        }

        IEnumerable<TrackedLine> trackedLines = await trackedLineRepository.GetByUserIdAsync(userId.Value);
        return Ok(trackedLines);
    }
}
