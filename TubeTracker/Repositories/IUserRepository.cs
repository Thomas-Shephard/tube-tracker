using System.Data;
using TubeTracker.API.Models.Entities;

namespace TubeTracker.API.Repositories;

public interface IUserRepository
{
    Task<User?> GetUserByEmailAsync(string email, IDbTransaction? transaction = null);
    Task<User?> GetUserByIdAsync(int userId, IDbTransaction? transaction = null);
    Task<int> CreateUserAsync(string email, string name, string passwordHash, IDbTransaction? transaction = null);
    Task UpdateUserAsync(User user, IDbTransaction? transaction = null);
    Task DeleteUserAsync(int userId, IDbTransaction? transaction = null);
}
