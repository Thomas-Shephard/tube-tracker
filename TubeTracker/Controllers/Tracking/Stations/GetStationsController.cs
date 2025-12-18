using Microsoft.AspNetCore.Mvc;
using TubeTracker.API.Models.Entities;
using TubeTracker.API.Repositories;

namespace TubeTracker.API.Controllers.Tracking.Stations;

[ApiController]
[Route("api/stations")]
[Tags("Stations")]
public class GetStationsController(IStationRepository stationRepository) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAllStations()
    {
        IEnumerable<Station> stations = await stationRepository.GetAllAsync();
        return Ok(stations);
    }
}
