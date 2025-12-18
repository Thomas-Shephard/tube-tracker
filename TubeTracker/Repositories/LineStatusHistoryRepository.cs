using System.Data;
using Dapper;
using TubeTracker.API.Models.Entities;
using TubeTracker.API.Settings;

namespace TubeTracker.API.Repositories;

public class LineStatusHistoryRepository(IDbConnection connection, StatusBackgroundSettings settings) : ILineStatusHistoryRepository
{
    public async Task UpsertAsync(int lineId, int statusSeverity, DateTime? threshold = null)
    {
        const string findQuery = """
                                 SELECT history_id FROM LineStatusHistory 
                                 WHERE line_id = @LineId 
                                   AND status_severity = @StatusSeverity 
                                   AND last_reported_at >= @Threshold
                                 ORDER BY last_reported_at DESC 
                                 LIMIT 1
                                 """;

        DateTime actualThreshold = threshold ?? DateTime.UtcNow.AddMinutes(-settings.DeduplicationThresholdMinutes);
        int? historyId = await connection.QueryFirstOrDefaultAsync<int?>(findQuery, new { LineId = lineId, StatusSeverity = statusSeverity, Threshold = actualThreshold });

        if (historyId.HasValue)
        {
            const string updateQuery = "UPDATE LineStatusHistory SET last_reported_at = @Now WHERE history_id = @HistoryId";
            await connection.ExecuteAsync(updateQuery, new { Now = DateTime.UtcNow, HistoryId = historyId.Value });
        }
        else
        {
            const string insertQuery = "INSERT INTO LineStatusHistory (line_id, status_severity, first_reported_at, last_reported_at) VALUES (@LineId, @StatusSeverity, @Now, @Now)";
            await connection.ExecuteAsync(insertQuery, new { LineId = lineId, StatusSeverity = statusSeverity, Now = DateTime.UtcNow });
        }
    }

    public async Task<IEnumerable<LineStatusHistory>> GetActiveByLineIdAsync(int lineId)
    {
        const string query = """
                             SELECT lsh.history_id, lsh.line_id, lsh.status_severity, lsh.first_reported_at, lsh.last_reported_at,
                                    ss.severity_level, ss.description, ss.urgency
                             FROM LineStatusHistory lsh
                             JOIN StatusSeverity ss ON lsh.status_severity = ss.severity_level
                             WHERE lsh.line_id = @LineId 
                               AND lsh.last_reported_at >= @Threshold
                             ORDER BY lsh.last_reported_at DESC
                             """;

        DateTime threshold = DateTime.UtcNow.AddMinutes(-settings.DeduplicationThresholdMinutes);
        IEnumerable<dynamic> rows = await connection.QueryAsync<dynamic>(query, new { LineId = lineId, Threshold = threshold });

        return rows.Select(row => new LineStatusHistory
        {
            HistoryId = (int)row.history_id,
            LineId = (int)row.line_id,
            StatusSeverity = (int)row.status_severity,
            FirstReportedAt = (DateTime)row.first_reported_at,
            LastReportedAt = (DateTime)row.last_reported_at,
            Severity = new StatusSeverity
            {
                SeverityLevel = (int)row.severity_level,
                Description = (string)row.description,
                Urgency = (int)row.urgency
            }
        });
    }

    public async Task<DateTime?> GetLastReportTimeAsync()
    {
        const string query = "SELECT MAX(last_reported_at) FROM LineStatusHistory";
        return await connection.QueryFirstOrDefaultAsync<DateTime?>(query);
    }
}
