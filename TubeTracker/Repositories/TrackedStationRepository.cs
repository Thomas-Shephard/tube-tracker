using System.Data;
using Dapper;
using TubeTracker.API.Models.Entities;

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

    public async Task<IEnumerable<TrackedStation>> GetAllAsync()
    {
        const string query = "SELECT * FROM TrackedStation";
        return await connection.QueryAsync<TrackedStation>(query);
    }
}
