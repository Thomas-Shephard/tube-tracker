using System.Data;
using Dapper;
using TubeTracker.API.Models.Entities;
using TubeTracker.API.Models.Notifications;

namespace TubeTracker.API.Repositories;

public class TrackedStationRepository(IDbConnection connection) : ITrackedStationRepository
{
    public async Task<IEnumerable<TrackedStation>> GetByUserIdAsync(int userId, IDbTransaction? transaction = null)
    {
        const string query = "SELECT * FROM TrackedStation WHERE user_id = @UserId";
        return await connection.QueryAsync<TrackedStation>(query, new { UserId = userId }, transaction);
    }

    public async Task<TrackedStation?> GetAsync(int userId, int stationId, IDbTransaction? transaction = null)
    {
        const string query = "SELECT * FROM TrackedStation WHERE user_id = @UserId AND station_id = @StationId";
        return await connection.QuerySingleOrDefaultAsync<TrackedStation>(query, new { UserId = userId, StationId = stationId }, transaction);
    }

    public async Task AddAsync(TrackedStation trackedStation, IDbTransaction? transaction = null)
    {
        const string query = "INSERT INTO TrackedStation (user_id, station_id, notify, notify_accessibility, min_urgency, created_at) VALUES (@UserId, @StationId, @Notify, @NotifyAccessibility, @MinUrgency, @CreatedAt)";
        await connection.ExecuteAsync(query, trackedStation, transaction);
    }

    public async Task UpdateAsync(TrackedStation trackedStation, IDbTransaction? transaction = null)
    {
        const string query = "UPDATE TrackedStation SET notify = @Notify, notify_accessibility = @NotifyAccessibility, min_urgency = @MinUrgency, last_notified_at = @LastNotifiedAt WHERE user_id = @UserId AND station_id = @StationId";
        await connection.ExecuteAsync(query, trackedStation, transaction);
    }

    public async Task DeleteAsync(int userId, int stationId, IDbTransaction? transaction = null)
    {
        const string query = "DELETE FROM TrackedStation WHERE user_id = @UserId AND station_id = @StationId";
        await connection.ExecuteAsync(query, new { UserId = userId, StationId = stationId }, transaction);
    }

    public async Task<IEnumerable<StationNotificationModel>> GetPendingNotificationsAsync()
    {
        const string query = """
                             SELECT 
                                 ts.tracked_station_id AS TrackedStationId,
                                 ssh.history_id AS HistoryId,
                                 u.email AS UserEmail,
                                 u.name AS UserName,
                                 s.common_name AS StationName,
                                 ssh.status_description AS StatusDescription,
                                 ssh.first_reported_at AS ReportedAt
                             FROM TrackedStation ts
                             JOIN User u ON ts.user_id = u.user_id
                             JOIN Station s ON ts.station_id = s.station_id
                             JOIN StationStatusHistory ssh ON s.station_id = ssh.station_id
                             WHERE ts.notify = 1
                               AND u.is_verified = 1
                               AND ssh.last_reported_at > DATE_SUB(UTC_TIMESTAMP(), INTERVAL 15 MINUTE)
                               AND (max_notified_history_id IS NULL OR ssh.history_id > max_notified_history_id)
                               AND ssh.status_description != 'No Issues'
                             """;

        return await connection.QueryAsync<StationNotificationModel>(query);
    }

    public async Task UpdateLastNotifiedAsync(int trackedStationId, int historyId, DateTime lastNotifiedAt)
    {
        const string query = "UPDATE TrackedStation SET last_notified_at = @LastNotifiedAt, max_notified_history_id = @HistoryId WHERE tracked_station_id = @TrackedStationId";
        await connection.ExecuteAsync(query, new { TrackedStationId = trackedStationId, HistoryId = historyId, LastNotifiedAt = lastNotifiedAt });
    }
}
