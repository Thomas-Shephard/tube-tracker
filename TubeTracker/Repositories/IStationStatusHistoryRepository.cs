using TubeTracker.API.Models.Entities;

namespace TubeTracker.API.Repositories;

public interface IStationStatusHistoryRepository
{
    Task UpsertAsync(int stationId, string statusDescription, DateTime? threshold = null);
    Task<IEnumerable<StationStatusHistory>> GetActiveByStationIdAsync(int stationId);
    Task<DateTime?> GetLastReportTimeAsync();
    Task<int> DeleteOldHistoryAsync(DateTime threshold);
}
