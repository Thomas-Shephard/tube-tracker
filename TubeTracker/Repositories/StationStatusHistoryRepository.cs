using System.Data;
using Dapper;
using TubeTracker.API.Models.Entities;
using TubeTracker.API.Settings;

namespace TubeTracker.API.Repositories;

public class StationStatusHistoryRepository(IDbConnection connection, StatusBackgroundSettings settings) : IStationStatusHistoryRepository
{
    public async Task UpsertAsync(int stationId, string statusDescription, DateTime? threshold = null)
    {
        const string findQuery = """
                                 SELECT history_id FROM StationStatusHistory 
                                 WHERE station_id = @StationId 
                                   AND status_description = @StatusDescription 
                                   AND last_reported_at >= @Threshold
                                 ORDER BY last_reported_at DESC 
                                 LIMIT 1
                                 """;

        DateTime actualThreshold = threshold ?? DateTime.UtcNow.AddMinutes(-settings.DeduplicationThresholdMinutes);
        int? historyId = await connection.QueryFirstOrDefaultAsync<int?>(findQuery, new { StationId = stationId, StatusDescription = statusDescription, Threshold = actualThreshold });

        if (historyId.HasValue)
        {
            const string updateQuery = "UPDATE StationStatusHistory SET last_reported_at = @Now WHERE history_id = @HistoryId";
            await connection.ExecuteAsync(updateQuery, new { Now = DateTime.UtcNow, HistoryId = historyId.Value });
        }
        else
        {
            const string insertQuery = "INSERT INTO StationStatusHistory (station_id, status_description, first_reported_at, last_reported_at) VALUES (@StationId, @StatusDescription, @Now, @Now)";
            await connection.ExecuteAsync(insertQuery, new { StationId = stationId, StatusDescription = statusDescription, Now = DateTime.UtcNow });
        }
    }

    public async Task<IEnumerable<StationStatusHistory>> GetActiveByStationIdAsync(int stationId)
    {
        const string query = """
                             SELECT history_id AS HistoryId, 
                                    station_id AS StationId, 
                                    status_description AS StatusDescription, 
                                    first_reported_at AS FirstReportedAt, 
                                    last_reported_at AS LastReportedAt 
                             FROM StationStatusHistory 
                             WHERE station_id = @StationId 
                               AND last_reported_at >= @Threshold
                             ORDER BY last_reported_at DESC
                             """;

        DateTime threshold = DateTime.UtcNow.AddMinutes(-settings.DeduplicationThresholdMinutes);
        return await connection.QueryAsync<StationStatusHistory>(query, new { StationId = stationId, Threshold = threshold });
    }

    public async Task<DateTime?> GetLastReportTimeAsync()
    {
        const string query = "SELECT MAX(last_reported_at) FROM StationStatusHistory";
        return await connection.QueryFirstOrDefaultAsync<DateTime?>(query);
    }

    public async Task<int> DeleteOldHistoryAsync(DateTime threshold)
    {
        const string query = "DELETE FROM StationStatusHistory WHERE last_reported_at < @Threshold";
        return await connection.ExecuteAsync(query, new { Threshold = threshold });
    }
}
