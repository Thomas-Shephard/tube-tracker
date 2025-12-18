using TubeTracker.API.Models.Entities;

namespace TubeTracker.API.Repositories;

public interface ILineStatusHistoryRepository
{
    Task UpsertAsync(int lineId, int statusSeverity, DateTime? threshold = null);
    Task<IEnumerable<LineStatusHistory>> GetActiveByLineIdAsync(int lineId);
    Task<DateTime?> GetLastReportTimeAsync();
}
