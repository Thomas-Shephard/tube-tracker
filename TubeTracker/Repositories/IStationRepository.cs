using TubeTracker.API.Models.Entities;

namespace TubeTracker.API.Repositories;

public interface IStationRepository
{
    Task<Station?> GetByIdAsync(int stationId);
    Task<Station?> GetByTflIdAsync(string tflId);
    Task<Station?> GetByCommonNameAsync(string commonName);
    Task<IEnumerable<Station>> GetAllAsync();
    Task AddAsync(Station station);
    Task UpdateAsync(Station station);
    Task DeleteAsync(int stationId);
}
