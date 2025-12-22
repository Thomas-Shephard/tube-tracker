using TubeTracker.API.Models.Entities;

namespace TubeTracker.API.Repositories;

public interface IStationStatusSeverityRepository
{
    Task<IEnumerable<StationStatusSeverity>> GetAllAsync();
}
