using TubeTracker.API.Models.Entities;

namespace TubeTracker.API.Repositories;

public interface IStationStatusHistoryRepository
{
    Task<StationStatusHistory?> GetActiveHistoryAsync(int stationId, string statusDescription, DateTime threshold);
    Task UpdateLastReportedAsync(int historyId);
    Task UpdateIsFutureAsync(int historyId, bool isFuture);
    Task InsertAsync(int stationId, string statusDescription, int statusSeverityId, bool isFuture);
    Task<IEnumerable<StationStatusHistory>> GetActiveByStationIdAsync(int stationId);
    Task<IEnumerable<StationStatusHistory>> GetAllActiveHistoryAsync(DateTime threshold);
    Task<IEnumerable<string>> GetDistinctDescriptionsBySeverityAsync(int severityId);
    Task UpdateClassificationByDescriptionAsync(string description, int pendingSeverityId, int targetSeverityId, bool isFuture);
    Task<DateTime?> GetLastReportTimeAsync();
    Task<int> DeleteOldHistoryAsync(DateTime threshold);
}