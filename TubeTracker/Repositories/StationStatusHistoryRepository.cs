using System.Data;
using Dapper;
using TubeTracker.API.Models.Entities;
using TubeTracker.API.Settings;

namespace TubeTracker.API.Repositories;

public class StationStatusHistoryRepository(IDbConnection connection, StatusBackgroundSettings settings) : IStationStatusHistoryRepository
{
    public async Task<int?> TryGetActiveHistoryIdAsync(int stationId, string statusDescription, DateTime threshold)
    {
        const string findQuery = """
                                 SELECT history_id FROM StationStatusHistory 
                                 WHERE station_id = @StationId 
                                   AND status_description = @StatusDescription 
                                   AND last_reported_at >= @Threshold
                                 ORDER BY last_reported_at DESC 
                                 LIMIT 1
                                 """;
        return await connection.QueryFirstOrDefaultAsync<int?>(findQuery, new { StationId = stationId, StatusDescription = statusDescription, Threshold = threshold });
    }

    public async Task UpdateLastReportedAsync(int historyId)
    {
        const string updateQuery = "UPDATE StationStatusHistory SET last_reported_at = @Now WHERE history_id = @HistoryId";
        await connection.ExecuteAsync(updateQuery, new { Now = DateTime.UtcNow, HistoryId = historyId });
    }

    public async Task InsertAsync(int stationId, string statusDescription, int statusSeverityId)
    {
        const string insertQuery = """
                                   INSERT INTO StationStatusHistory (station_id, status_description, status_severity_id, first_reported_at, last_reported_at) 
                                   VALUES (@StationId, @StatusDescription, @StatusSeverityId, @Now, @Now)
                                   """;
        await connection.ExecuteAsync(insertQuery, new
        {
            StationId = stationId,
            StatusDescription = statusDescription,
            StatusSeverityId = statusSeverityId,
            Now = DateTime.UtcNow
        });
    }

    public async Task<IEnumerable<StationStatusHistory>> GetActiveByStationIdAsync(int stationId)
    {
        const string query = """
                             SELECT ssh.history_id AS HistoryId, 
                                    ssh.station_id AS StationId, 
                                    ssh.status_description AS StatusDescription, 
                                    ssh.status_severity_id AS StatusSeverityId,
                                    ssh.first_reported_at AS FirstReportedAt, 
                                    ssh.last_reported_at AS LastReportedAt,
                                    sss.severity_id AS SeverityId,
                                    sss.description AS Description,
                                    sss.urgency AS Urgency,
                                    sss.is_accessibility AS IsAccessibility
                             FROM StationStatusHistory ssh
                             JOIN StationStatusSeverity sss ON ssh.status_severity_id = sss.severity_id
                             WHERE ssh.station_id = @StationId 
                               AND ssh.last_reported_at >= @Threshold
                             ORDER BY ssh.last_reported_at DESC
                             """;

        DateTime threshold = DateTime.UtcNow.AddMinutes(-settings.DeduplicationThresholdMinutes);
        return await connection.QueryAsync<StationStatusHistory, StationStatusSeverity, StationStatusHistory>(
            query, 
            (history, severity) => 
            {
                history.Severity = severity;
                return history;
            },
            new { StationId = stationId, Threshold = threshold },
            splitOn: "SeverityId"
        );
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