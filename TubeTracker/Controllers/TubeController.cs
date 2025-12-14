using Microsoft.AspNetCore.Mvc;
using TubeTracker.API.Models;
using TubeTracker.API.Services;

namespace TubeTracker.API.Controllers;

[ApiController]
[Route("api/tube")]
public class TubeController : ControllerBase
{
    private readonly ITflService _tflService;

    public TubeController(ITflService tflService)
    {
        _tflService = tflService;
    }

    [HttpGet("status")]
    public async Task<ActionResult<List<TflLine>>> GetStatus()
    {
        var statuses = await _tflService.GetTubeStatusesAsync();
        return Ok(statuses);
    }
}
