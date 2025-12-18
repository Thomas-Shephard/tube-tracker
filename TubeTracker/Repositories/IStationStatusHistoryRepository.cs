using TubeTracker.API.Models.Entities;

namespace TubeTracker.API.Repositories;

public interface IStationStatusHistoryRepository
{
    Task AddAsync(int stationId, string statusDescription);
    Task<StationStatusHistory?> GetLatestByStationIdAsync(int stationId);
}
