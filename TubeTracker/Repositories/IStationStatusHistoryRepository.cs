using TubeTracker.API.Models.Entities;

namespace TubeTracker.API.Repositories;

public interface IStationStatusHistoryRepository
{
    Task<StationStatusHistory?> GetActiveHistoryAsync(int stationId, string statusDescription, DateTime threshold);
    Task UpdateLastReportedAsync(int historyId);
    Task UpdateIsFutureAsync(int historyId, bool isFuture);
    Task InsertAsync(int stationId, string statusDescription, int statusSeverityId, bool isFuture, DateTime? validFrom, DateTime? validUntil);
    Task<IEnumerable<StationStatusHistory>> GetActiveByStationIdAsync(int stationId);
    Task<DateTime?> GetLastReportTimeAsync();
    Task<int> DeleteOldHistoryAsync(DateTime threshold);
}