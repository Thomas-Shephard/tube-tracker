using Microsoft.AspNetCore.Mvc;
using TubeTracker.API.Models.Entities;
using TubeTracker.API.Repositories;

namespace TubeTracker.API.Controllers.Tracking.Lines;

[ApiController]
[Route("api/lines")]
[Tags("Lines")]
public class GetLinesController(ILineRepository lineRepository) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAllLines()
    {
        IEnumerable<Line> lines = await lineRepository.GetAllAsync();
        return Ok(lines);
    }
}
