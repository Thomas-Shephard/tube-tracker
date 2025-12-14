using System.Data;
using Dapper;
using TubeTracker.API.Models.Entities;

namespace TubeTracker.API.Repositories;

public class UserVerificationRepository(IDbConnection connection) : IUserVerificationRepository
{
    public async Task CreateTokenAsync(int userId, string tokenHash, IDbTransaction? transaction = null)
    {
        const string query = "INSERT INTO UserVerificationToken (user_id, token_hash) VALUES (@UserId, @TokenHash)";
        await connection.ExecuteAsync(query, new { UserId = userId, TokenHash = tokenHash }, transaction);
    }

    public async Task<UserVerificationToken?> GetTokenByUserIdAsync(int userId, IDbTransaction? transaction = null)
    {
        const string query = "SELECT * FROM UserVerificationToken WHERE user_id = @UserId ORDER BY created_at DESC LIMIT 1";
        return await connection.QuerySingleOrDefaultAsync<UserVerificationToken>(query, new { UserId = userId }, transaction);
    }
}
