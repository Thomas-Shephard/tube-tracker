using System.Data;
using TubeTracker.API.Models.Entities;

namespace TubeTracker.API.Repositories;

public interface IUserRepository
{
    Task<User?> GetUserByEmailAsync(string email, IDbTransaction? transaction = null);
    Task UpdateUserAsync(User user, IDbTransaction? transaction = null);
}
