using System.Data;
using Dapper;
using TubeTracker.API.Models.Entities;

namespace TubeTracker.API.Repositories;

public class StationStatusSeverityRepository(IDbConnection connection) : IStationStatusSeverityRepository
{
    public async Task<IEnumerable<StationStatusSeverity>> GetAllAsync()
    {
        return await connection.QueryAsync<StationStatusSeverity>("SELECT * FROM StationStatusSeverity");
    }
}
