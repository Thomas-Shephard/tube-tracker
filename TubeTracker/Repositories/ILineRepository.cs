using TubeTracker.API.Models.Entities;

namespace TubeTracker.API.Repositories;

public interface ILineRepository
{
    Task<Line?> GetByIdAsync(int lineId);
    Task<IEnumerable<Line>> GetByIdsAsync(IEnumerable<int> lineIds);
    Task<Line?> GetByTflIdAsync(string tflId);
    Task<IEnumerable<Line>> GetAllAsync();
    Task AddAsync(Line line);
    Task UpdateAsync(Line line);
    Task DeleteAsync(int lineId);
}
