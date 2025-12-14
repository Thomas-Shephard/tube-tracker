using System.Data;
using Dapper;
using TubeTracker.API.Models.Entities;

namespace TubeTracker.API.Repositories;

public class UserRepository(IDbConnection connection) : IUserRepository
{
    public async Task<User?> GetUserByEmailAsync(string email, IDbTransaction? transaction = null)
    {
        const string query = "SELECT * FROM User WHERE email = @Email";
        return await connection.QuerySingleOrDefaultAsync<User>(query, new { Email = email }, transaction);
    }

    public async Task<User?> GetUserByIdAsync(int userId, IDbTransaction? transaction = null)
    {
        const string query = "SELECT * FROM User WHERE user_id = @UserId";
        return await connection.QuerySingleOrDefaultAsync<User>(query, new { UserId = userId }, transaction);
    }

    public async Task UpdateUserAsync(User user, IDbTransaction? transaction = null)
    {
        const string query = "UPDATE User SET email = @Email,  name = @Name, password_hash = @PasswordHash, last_login = @LastLogin, is_verified = @IsVerified WHERE user_id = @UserId;";
        await connection.ExecuteAsync(query, user, transaction);
    }

    public async Task<int> CreateUserAsync(string email, string name, string passwordHash, IDbTransaction? transaction = null)
    {
        const string query = "INSERT INTO User (email, name, password_hash) VALUES (@Email, @Name, @PasswordHash); SELECT LAST_INSERT_ID();";
        return await connection.ExecuteScalarAsync<int>(query, new { Email = email, Name = name, PasswordHash = passwordHash }, transaction);
    }

    public async Task DeleteUserAsync(int userId, IDbTransaction? transaction = null)
    {
        const string query = "DELETE FROM User WHERE user_id = @UserId";
        await connection.ExecuteAsync(query, new { UserId = userId }, transaction);
    }
}

