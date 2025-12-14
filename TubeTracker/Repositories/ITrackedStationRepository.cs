using System.Data;
using TubeTracker.API.Models.Entities;

namespace TubeTracker.API.Repositories;

public interface ITrackedStationRepository
{
    Task<IEnumerable<TrackedStation>> GetByUserIdAsync(int userId, IDbTransaction? transaction = null);
    Task<TrackedStation?> GetAsync(int userId, int stationId, IDbTransaction? transaction = null);
    Task AddAsync(TrackedStation trackedStation, IDbTransaction? transaction = null);
    Task UpdateAsync(TrackedStation trackedStation, IDbTransaction? transaction = null);
    Task DeleteAsync(int userId, int stationId, IDbTransaction? transaction = null);
}
