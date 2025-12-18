using TubeTracker.API.Models.Entities;

namespace TubeTracker.API.Repositories;

public interface IStationStatusHistoryRepository
{
    Task AddAsync(StationStatusHistory stationStatusHistory);
    Task<StationStatusHistory?> GetLatestByStationIdAsync(int stationId);
}
