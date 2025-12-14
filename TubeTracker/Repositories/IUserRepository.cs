using TubeTracker.API.Models.Entities;

namespace TubeTracker.API.Repositories;

public interface IUserRepository
{
    Task<User?> GetUserByEmailAsync(string email);
    Task UpdateUserAsync(User user);
}
