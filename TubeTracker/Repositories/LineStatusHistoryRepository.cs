using System.Data;
using Dapper;
using TubeTracker.API.Models.Entities;

namespace TubeTracker.API.Repositories;

public class LineStatusHistoryRepository(IDbConnection connection) : ILineStatusHistoryRepository
{
    public async Task AddAsync(int lineId, int statusSeverity)
    {
        const string query = "INSERT INTO LineStatusHistory (line_id, status_severity) VALUES (@LineId, @StatusSeverity)";
        await connection.ExecuteAsync(query, new { LineId = lineId, StatusSeverity = statusSeverity });
    }

    public async Task<LineStatusHistory?> GetLatestByLineIdAsync(int lineId)
    {
        const string query = """
                             SELECT lsh.history_id, lsh.line_id, lsh.status_severity, lsh.checked_at,
                                    ss.severity_level, ss.description, ss.urgency
                             FROM LineStatusHistory lsh
                             JOIN StatusSeverity ss ON lsh.status_severity = ss.severity_level
                             WHERE lsh.line_id = @LineId 
                             ORDER BY lsh.checked_at DESC 
                             LIMIT 1
                             """;

        dynamic? row = await connection.QueryFirstOrDefaultAsync<dynamic>(query, new { LineId = lineId });

        if (row == null) return null;

        return new LineStatusHistory
        {
            HistoryId = (int)row.history_id,
            LineId = (int)row.line_id,
            StatusSeverity = (int)row.status_severity,
            CheckedAt = (DateTime)row.checked_at,
            Severity = new StatusSeverity
            {
                SeverityLevel = (int)row.severity_level,
                Description = (string)row.description,
                Urgency = (int)row.urgency
            }
        };
    }
}
