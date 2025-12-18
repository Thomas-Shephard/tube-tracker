using TubeTracker.API.Models.Entities;

namespace TubeTracker.API.Repositories;

public interface ILineStatusHistoryRepository
{
    Task AddAsync(int lineId, int statusSeverity);
    Task<LineStatusHistory?> GetLatestByLineIdAsync(int lineId);
}
