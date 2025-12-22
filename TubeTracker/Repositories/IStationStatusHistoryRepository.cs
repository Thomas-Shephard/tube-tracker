using TubeTracker.API.Models.Entities;

namespace TubeTracker.API.Repositories;

public interface IStationStatusHistoryRepository
{
    Task<int?> TryGetActiveHistoryIdAsync(int stationId, string statusDescription, DateTime threshold);
    Task UpdateLastReportedAsync(int historyId);
    Task InsertAsync(int stationId, string statusDescription, int statusSeverityId);
    Task<IEnumerable<StationStatusHistory>> GetActiveByStationIdAsync(int stationId);
    Task<DateTime?> GetLastReportTimeAsync();
    Task<int> DeleteOldHistoryAsync(DateTime threshold);
}