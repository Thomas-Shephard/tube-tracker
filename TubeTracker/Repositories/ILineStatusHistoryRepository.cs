using TubeTracker.API.Models.Entities;

namespace TubeTracker.API.Repositories;

public interface ILineStatusHistoryRepository
{
    Task UpsertAsync(int lineId, int statusSeverity);
    Task<IEnumerable<LineStatusHistory>> GetActiveByLineIdAsync(int lineId);
}
