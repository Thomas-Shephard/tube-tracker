using System.Data;
using Dapper;
using TubeTracker.API.Models.Entities;

namespace TubeTracker.API.Repositories;

public class LineRepository(IDbConnection connection) : ILineRepository
{
    public async Task<Line?> GetByIdAsync(int lineId)
    {
        const string query = "SELECT * FROM Line WHERE line_id = @LineId";
        return await connection.QuerySingleOrDefaultAsync<Line>(query, new { LineId = lineId });
    }

    public async Task<IEnumerable<Line>> GetByIdsAsync(IEnumerable<int> lineIds)
    {
        const string query = "SELECT * FROM Line WHERE line_id IN @LineIds";
        return await connection.QueryAsync<Line>(query, new { LineIds = lineIds });
    }

    public async Task<Line?> GetByTflIdAsync(string tflId)
    {
        const string query = "SELECT * FROM Line WHERE tfl_id = @TflId";
        return await connection.QuerySingleOrDefaultAsync<Line>(query, new { TflId = tflId });
    }

    public async Task<IEnumerable<Line>> GetAllAsync()
    {
        const string query = "SELECT * FROM Line";
        return await connection.QueryAsync<Line>(query);
    }

    public async Task AddAsync(Line line)
    {
        const string query = "INSERT INTO Line (tfl_id, name, mode_name, colour) VALUES (@TflId, @Name, @ModeName, @Colour)";
        await connection.ExecuteAsync(query, line);
    }

    public async Task UpdateAsync(Line line)
    {
        const string query = "UPDATE Line SET tfl_id = @TflId, name = @Name, mode_name = @ModeName, colour = @Colour WHERE line_id = @LineId";
        await connection.ExecuteAsync(query, line);
    }

    public async Task DeleteAsync(int lineId)
    {
        const string query = "DELETE FROM Line WHERE line_id = @LineId";
        await connection.ExecuteAsync(query, new { LineId = lineId });
    }
}
