using TubeTracker.API.Models.Entities;

namespace TubeTracker.API.Repositories;

public interface IStationStatusHistoryRepository
{
    Task UpsertAsync(int stationId, string statusDescription);
    Task<IEnumerable<StationStatusHistory>> GetActiveByStationIdAsync(int stationId);
}
