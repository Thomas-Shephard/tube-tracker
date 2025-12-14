using System.Data;
using Dapper;
using TubeTracker.API.Models.Entities;

namespace TubeTracker.API.Repositories;

public class UserRepository(IDbConnection connection) : IUserRepository
{
    public async Task<User?> GetUserByEmailAsync(string email)
    {
        const string query = "SELECT * FROM User WHERE email = @Email";
        return await connection.QuerySingleOrDefaultAsync<User>(query, new { Email = email });
    }

    public async Task UpdateUserAsync(User user)
    {
        const string query = "UPDATE User SET email = @Email,  name = @Name, password_hash = @PasswordHash, last_login = @LastLogin WHERE user_id = @UserId;";
        await connection.ExecuteAsync(query, user);
    }
}
