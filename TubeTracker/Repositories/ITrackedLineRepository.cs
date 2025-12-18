using System.Data;
using TubeTracker.API.Models.Entities;
using TubeTracker.API.Models.Notifications;

namespace TubeTracker.API.Repositories;

public interface ITrackedLineRepository
{
    Task<IEnumerable<TrackedLine>> GetByUserIdAsync(int userId, IDbTransaction? transaction = null);
    Task<TrackedLine?> GetAsync(int userId, int lineId, IDbTransaction? transaction = null);
    Task AddAsync(TrackedLine trackedLine, IDbTransaction? transaction = null);
    Task UpdateAsync(TrackedLine trackedLine, IDbTransaction? transaction = null);
    Task DeleteAsync(int userId, int lineId, IDbTransaction? transaction = null);
    Task<IEnumerable<LineNotificationModel>> GetPendingNotificationsAsync();
    Task UpdateLastNotifiedAsync(int trackedLineId, int historyId, DateTime lastNotifiedAt);
}
