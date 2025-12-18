using System.Data;
using Dapper;
using TubeTracker.API.Models.Entities;

namespace TubeTracker.API.Repositories;

public class StationStatusHistoryRepository(IDbConnection connection) : IStationStatusHistoryRepository
{
    public async Task AddAsync(int stationId, string statusDescription)
    {
        const string query = "INSERT INTO StationStatusHistory (station_id, status_description) VALUES (@StationId, @StatusDescription)";
        await connection.ExecuteAsync(query, new { StationId = stationId, StatusDescription = statusDescription });
    }

    public async Task<StationStatusHistory?> GetLatestByStationIdAsync(int stationId)
    {
        const string query = "SELECT * FROM StationStatusHistory WHERE station_id = @StationId ORDER BY checked_at DESC";
        return await connection.QueryFirstOrDefaultAsync<StationStatusHistory>(query, new { StationId = stationId });
    }
}
