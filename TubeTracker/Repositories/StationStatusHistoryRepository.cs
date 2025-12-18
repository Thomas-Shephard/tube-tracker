using System.Data;
using Dapper;
using TubeTracker.API.Models.Entities;

namespace TubeTracker.API.Repositories;

public class StationStatusHistoryRepository(IDbConnection connection) : IStationStatusHistoryRepository
{
    public async Task AddAsync(StationStatusHistory stationStatusHistory)
    {
        const string query = "INSERT INTO StationStatusHistory (station_id, status_description, checked_at) VALUES (@StationId, @StatusDescription, @CheckedAt)";
        await connection.ExecuteAsync(query, stationStatusHistory);
    }

    public async Task<StationStatusHistory?> GetLatestByStationIdAsync(int stationId)
    {
        const string query = "SELECT * FROM StationStatusHistory WHERE station_id = @StationId ORDER BY checked_at DESC";
        return await connection.QueryFirstOrDefaultAsync<StationStatusHistory>(query, new { StationId = stationId });
    }
}
