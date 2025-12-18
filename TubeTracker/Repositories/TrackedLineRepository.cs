using System.Data;
using Dapper;
using TubeTracker.API.Models.Entities;
using TubeTracker.API.Models.Notifications;

namespace TubeTracker.API.Repositories;

public class TrackedLineRepository(IDbConnection connection) : ITrackedLineRepository
{
    public async Task<IEnumerable<TrackedLine>> GetByUserIdAsync(int userId, IDbTransaction? transaction = null)
    {
        const string query = "SELECT * FROM TrackedLine WHERE user_id = @UserId";
        return await connection.QueryAsync<TrackedLine>(query, new { UserId = userId }, transaction);
    }

    public async Task<TrackedLine?> GetAsync(int userId, int lineId, IDbTransaction? transaction = null)
    {
        const string query = "SELECT * FROM TrackedLine WHERE user_id = @UserId AND line_id = @LineId";
        return await connection.QuerySingleOrDefaultAsync<TrackedLine>(query, new { UserId = userId, LineId = lineId }, transaction);
    }

    public async Task AddAsync(TrackedLine trackedLine, IDbTransaction? transaction = null)
    {
        const string query = "INSERT INTO TrackedLine (user_id, line_id, notify, min_urgency, created_at) VALUES (@UserId, @LineId, @Notify, @MinUrgency, @CreatedAt)";
        await connection.ExecuteAsync(query, trackedLine, transaction);
    }

    public async Task UpdateAsync(TrackedLine trackedLine, IDbTransaction? transaction = null)
    {
        const string query = "UPDATE TrackedLine SET notify = @Notify, min_urgency = @MinUrgency, last_notified_at = @LastNotifiedAt WHERE user_id = @UserId AND line_id = @LineId";
        await connection.ExecuteAsync(query, trackedLine, transaction);
    }

    public async Task DeleteAsync(int userId, int lineId, IDbTransaction? transaction = null)
    {
        const string query = "DELETE FROM TrackedLine WHERE user_id = @UserId AND line_id = @LineId";
        await connection.ExecuteAsync(query, new { UserId = userId, LineId = lineId }, transaction);
    }

    public async Task<IEnumerable<LineNotificationModel>> GetPendingNotificationsAsync()
    {
        const string query = """
                             SELECT 
                                 tl.tracked_line_id AS TrackedLineId,
                                 lsh.history_id AS HistoryId,
                                 u.email AS UserEmail,
                                 u.name AS UserName,
                                 l.name AS LineName,
                                 ss.description AS StatusDescription,
                                 lsh.first_reported_at AS ReportedAt
                             FROM TrackedLine tl
                             JOIN User u ON tl.user_id = u.user_id
                             JOIN Line l ON tl.line_id = l.line_id
                             JOIN LineStatusHistory lsh ON l.line_id = lsh.line_id
                             JOIN StatusSeverity ss ON lsh.status_severity = ss.severity_level
                             LEFT JOIN LineStatusHistory lsh_prev ON tl.max_notified_history_id = lsh_prev.history_id
                             LEFT JOIN StatusSeverity ss_prev ON lsh_prev.status_severity = ss_prev.severity_level
                             WHERE tl.notify = 1
                               AND u.is_verified = 1
                               AND lsh.last_reported_at > DATE_SUB(UTC_TIMESTAMP(), INTERVAL 15 MINUTE)
                               AND (tl.max_notified_history_id IS NULL OR lsh.history_id > tl.max_notified_history_id)
                               AND (ss.urgency >= IFNULL(tl.min_urgency, 2) OR (ss_prev.urgency IS NOT NULL AND ss.urgency < ss_prev.urgency))
                             """;

        return await connection.QueryAsync<LineNotificationModel>(query);
    }

    public async Task UpdateLastNotifiedAsync(int trackedLineId, int historyId, DateTime lastNotifiedAt)
    {
        const string query = "UPDATE TrackedLine SET last_notified_at = @LastNotifiedAt, max_notified_history_id = @HistoryId WHERE tracked_line_id = @TrackedLineId";
        await connection.ExecuteAsync(query, new { TrackedLineId = trackedLineId, HistoryId = historyId, LastNotifiedAt = lastNotifiedAt });
    }
}
