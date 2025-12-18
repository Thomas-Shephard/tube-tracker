using System.Data;
using Dapper;
using TubeTracker.API.Models.Entities;
using TubeTracker.API.Settings;

namespace TubeTracker.API.Repositories;

public class StationStatusHistoryRepository(IDbConnection connection, StatusBackgroundSettings settings) : IStationStatusHistoryRepository
{
    public async Task UpsertAsync(int stationId, string statusDescription)
    {
        const string findQuery = """
                                 SELECT history_id FROM StationStatusHistory 
                                 WHERE station_id = @StationId 
                                   AND status_description = @StatusDescription 
                                   AND last_checked_at >= @Threshold
                                 ORDER BY last_checked_at DESC 
                                 LIMIT 1
                                 """;

        DateTime threshold = DateTime.UtcNow.AddMinutes(-settings.DeduplicationThresholdMinutes);
        int? historyId = await connection.QueryFirstOrDefaultAsync<int?>(findQuery, new { StationId = stationId, StatusDescription = statusDescription, Threshold = threshold });

        if (historyId.HasValue)
        {
            const string updateQuery = "UPDATE StationStatusHistory SET last_checked_at = @Now WHERE history_id = @HistoryId";
            await connection.ExecuteAsync(updateQuery, new { Now = DateTime.UtcNow, HistoryId = historyId.Value });
        }
        else
        {
            const string insertQuery = "INSERT INTO StationStatusHistory (station_id, status_description, first_checked_at, last_checked_at) VALUES (@StationId, @StatusDescription, @Now, @Now)";
            await connection.ExecuteAsync(insertQuery, new { StationId = stationId, StatusDescription = statusDescription, Now = DateTime.UtcNow });
        }
    }

    public async Task<IEnumerable<StationStatusHistory>> GetActiveByStationIdAsync(int stationId)
    {
        const string query = """
                             SELECT * FROM StationStatusHistory 
                             WHERE station_id = @StationId 
                               AND last_checked_at >= @Threshold
                             ORDER BY last_checked_at DESC
                             """;

        DateTime threshold = DateTime.UtcNow.AddMinutes(-settings.DeduplicationThresholdMinutes);
        return await connection.QueryAsync<StationStatusHistory>(query, new { StationId = stationId, Threshold = threshold });
    }
}
