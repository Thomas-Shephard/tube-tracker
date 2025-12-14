using TubeTracker.API.Models.Entities;

namespace TubeTracker.API.Repositories;

public interface ITokenDenyRepository
{
    Task DenyTokenAsync(DeniedToken deniedToken);
    Task DeleteExpiredTokensAsync(DateTime now);
    Task<IEnumerable<DeniedToken>> GetActiveDeniedTokensAsync(DateTime now);
}
