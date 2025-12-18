using System.Data;
using Dapper;
using TubeTracker.API.Models.Entities;

namespace TubeTracker.API.Repositories;

public class StationRepository(IDbConnection connection) : IStationRepository
{
    public async Task<Station?> GetByIdAsync(int stationId)
    {
        const string query = "SELECT * FROM Station WHERE station_id = @StationId";
        return await connection.QuerySingleOrDefaultAsync<Station>(query, new { StationId = stationId });
    }

    public async Task<Station?> GetByTflIdAsync(string tflId)
    {
        const string query = "SELECT * FROM Station WHERE tfl_id = @TflId";
        return await connection.QuerySingleOrDefaultAsync<Station>(query, new { TflId = tflId });
    }

    public async Task<Station?> GetByCommonNameAsync(string commonName)
    {
        const string query = "SELECT * FROM Station WHERE common_name = @CommonName";
        return await connection.QueryFirstOrDefaultAsync<Station>(query, new { CommonName = commonName });
    }

    public async Task<IEnumerable<Station>> GetAllAsync()
    {
        const string query = "SELECT * FROM Station";
        return await connection.QueryAsync<Station>(query);
    }

    public async Task AddAsync(Station station)
    {
        const string query = "INSERT INTO Station (tfl_id, common_name, lat, lon) VALUES (@TflId, @CommonName, @Lat, @Lon)";
        await connection.ExecuteAsync(query, station);
    }

    public async Task UpdateAsync(Station station)
    {
        const string query = "UPDATE Station SET tfl_id = @TflId, common_name = @CommonName, lat = @Lat, lon = @Lon WHERE station_id = @StationId";
        await connection.ExecuteAsync(query, station);
    }

    public async Task DeleteAsync(int stationId)
    {
        const string query = "DELETE FROM Station WHERE station_id = @StationId";
        await connection.ExecuteAsync(query, new { StationId = stationId });
    }
}
