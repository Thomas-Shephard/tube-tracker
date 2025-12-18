using Microsoft.AspNetCore.Mvc;
using TubeTracker.API.Models.Entities;
using TubeTracker.API.Repositories;

namespace TubeTracker.API.Controllers.Tracking.Lines;

[ApiController]
[Route("api/status/lines")]
[Tags("Status")]
public class GetLineStatusesController(ILineRepository lineRepository, ILineStatusHistoryRepository lineStatusHistoryRepository) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAllLineStatuses()
    {
        IEnumerable<Line> lines = await lineRepository.GetAllAsync();
        List<object> results = [];

        foreach (Line line in lines)
        {
            IEnumerable<LineStatusHistory> activeStatuses = await lineStatusHistoryRepository.GetActiveByLineIdAsync(line.LineId);
            results.Add(new
            {
                line.LineId,
                line.Name,
                line.TflId,
                Statuses = activeStatuses
            });
        }

        return Ok(results);
    }
}
