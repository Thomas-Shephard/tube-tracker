using System.Data;
using Dapper;
using TubeTracker.API.Models.Entities;
using TubeTracker.API.Settings;

namespace TubeTracker.API.Repositories;

public class StationStatusHistoryRepository(IDbConnection connection, StatusBackgroundSettings settings) : IStationStatusHistoryRepository
{
    public async Task<StationStatusHistory?> GetActiveHistoryAsync(int stationId, string statusDescription, DateTime threshold)
    {
        const string findQuery = """
                                 SELECT history_id AS HistoryId,
                                        station_id AS StationId,
                                        status_description AS StatusDescription,
                                        status_severity_id AS StatusSeverityId,
                                        is_future AS IsFuture,
                                        first_reported_at AS FirstReportedAt,
                                        last_reported_at AS LastReportedAt
                                 FROM StationStatusHistory 
                                 WHERE station_id = @StationId 
                                   AND status_description = @StatusDescription 
                                   AND last_reported_at >= @Threshold
                                 ORDER BY last_reported_at DESC 
                                 LIMIT 1
                                 """;
        return await connection.QueryFirstOrDefaultAsync<StationStatusHistory?>(findQuery, new { StationId = stationId, StatusDescription = statusDescription, Threshold = threshold });
    }

    public async Task UpdateLastReportedAsync(int historyId)
    {
        const string updateQuery = "UPDATE StationStatusHistory SET last_reported_at = @Now WHERE history_id = @HistoryId";
        await connection.ExecuteAsync(updateQuery, new { Now = DateTime.UtcNow, HistoryId = historyId });
    }

    public async Task UpdateIsFutureAsync(int historyId, bool isFuture)
    {
        const string updateQuery = "UPDATE StationStatusHistory SET is_future = @IsFuture WHERE history_id = @HistoryId";
        await connection.ExecuteAsync(updateQuery, new { IsFuture = isFuture, HistoryId = historyId });
    }

    public async Task InsertAsync(int stationId, string statusDescription, int statusSeverityId, bool isFuture)
    {
        const string insertQuery = """
                                   INSERT INTO StationStatusHistory (station_id, status_description, status_severity_id, is_future, first_reported_at, last_reported_at) 
                                   VALUES (@StationId, @StatusDescription, @StatusSeverityId, @IsFuture, @Now, @Now)
                                   """;
        await connection.ExecuteAsync(insertQuery, new
        {
            StationId = stationId,
            StatusDescription = statusDescription,
            StatusSeverityId = statusSeverityId,
            IsFuture = isFuture,
            Now = DateTime.UtcNow
        });
    }

    public async Task<IEnumerable<StationStatusHistory>> GetActiveByStationIdAsync(int stationId)
    {
        const string query = """
                             SELECT ssh.*, sss.*
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
            splitOn: "severity_id"
        );
    }

    public async Task<IEnumerable<StationStatusHistory>> GetAllActiveHistoryAsync(DateTime threshold)
    {
        const string query = """
                             SELECT history_id AS HistoryId,
                                    station_id AS StationId,
                                    status_description AS StatusDescription,
                                    status_severity_id AS StatusSeverityId,
                                    is_future AS IsFuture,
                                    first_reported_at AS FirstReportedAt,
                                    last_reported_at AS LastReportedAt
                             FROM StationStatusHistory 
                             WHERE last_reported_at >= @Threshold
                             """;
        return await connection.QueryAsync<StationStatusHistory>(query, new { Threshold = threshold });
    }

    public async Task<IEnumerable<string>> GetDistinctDescriptionsBySeverityAsync(int severityId)
    {
        const string query = "SELECT DISTINCT status_description FROM StationStatusHistory WHERE status_severity_id = @SeverityId";
        return await connection.QueryAsync<string>(query, new { SeverityId = severityId });
    }

    public async Task UpdateClassificationByDescriptionAsync(string description, int pendingSeverityId, int targetSeverityId, bool isFuture)
    {
        const string query = """
                             UPDATE StationStatusHistory 
                             SET status_severity_id = @TargetSeverityId, is_future = @IsFuture 
                             WHERE status_description = @Description AND status_severity_id = @PendingSeverityId
                             """;
        await connection.ExecuteAsync(query, new
        {
            Description = description,
            PendingSeverityId = pendingSeverityId,
            TargetSeverityId = targetSeverityId,
            IsFuture = isFuture
        });
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
