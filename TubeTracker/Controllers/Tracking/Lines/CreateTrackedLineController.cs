using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TubeTracker.API.Attributes;
using TubeTracker.API.Extensions;
using TubeTracker.API.Models.Entities;
using TubeTracker.API.Models.Requests;
using TubeTracker.API.Repositories;

namespace TubeTracker.API.Controllers.Tracking.Lines;

[ApiController]
[Route("api/tracking/lines")]
[Tags("Tracking")]
[RequireVerifiedAccount]
public class CreateTrackedLineController(ITrackedLineRepository trackedLineRepository, ILogger<CreateTrackedLineController> logger) : ControllerBase
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
            logger.LogWarning("Unauthorized attempt to create tracked line - missing userId.");
            return Unauthorized("Token does not contain a sub claim.");
        }

        TrackedLine? existingTrackedLine = await trackedLineRepository.GetAsync(userId.Value, request.LineId);
        if (existingTrackedLine is not null)
        {
            logger.LogInformation("User {UserId} attempted to subscribe to already tracked line {LineId}", userId, request.LineId);
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

        logger.LogInformation("User {UserId} successfully subscribed to line {LineId}.", userId, request.LineId);

        return Ok(new { message = "Line subscription created successfully." });
    }
}
