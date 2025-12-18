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
        const string query = "SELECT * FROM LineStatusHistory WHERE line_id = @LineId ORDER BY checked_at DESC";
        return await connection.QueryFirstOrDefaultAsync<LineStatusHistory>(query, new { LineId = lineId });
    }
}
