using System.Data;
using Dapper;
using TubeTracker.API.Models.Entities;

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

    public async Task<IEnumerable<TrackedLine>> GetAllAsync()
    {
        const string query = "SELECT * FROM TrackedLine";
        return await connection.QueryAsync<TrackedLine>(query);
    }
}
