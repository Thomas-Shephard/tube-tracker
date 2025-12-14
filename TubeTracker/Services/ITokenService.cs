using TubeTracker.API.Models.Entities;

namespace TubeTracker.API.Services;

public interface ITokenService
{
    string GenerateToken(User user);
}
