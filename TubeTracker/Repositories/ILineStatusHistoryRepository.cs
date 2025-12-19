using TubeTracker.API.Models.Entities;

namespace TubeTracker.API.Repositories;

public interface ILineStatusHistoryRepository
{
    Task UpsertAsync(int lineId, int statusSeverity, string? statusDescription = null, DateTime? threshold = null);
    Task<IEnumerable<LineStatusHistory>> GetActiveByLineIdAsync(int lineId);
    Task<DateTime?> GetLastReportTimeAsync();
    Task<int> DeleteOldHistoryAsync(DateTime threshold);
}
