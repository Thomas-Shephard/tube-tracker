using System.Data;
using Dapper;
using TubeTracker.API.Models.Entities;

namespace TubeTracker.API.Repositories;

public class PasswordResetRepository(IDbConnection connection, IUserRepository userRepository) : IPasswordResetRepository
{
    public async Task CreateTokenAsync(int userId, string token)
    {
        using IDbTransaction transaction = connection.BeginTransaction();
        try
        {
            const string invalidateQuery = "UPDATE PasswordResetToken SET is_revoked = TRUE WHERE user_id = @UserId AND is_used = FALSE AND is_revoked = FALSE";
            await connection.ExecuteAsync(invalidateQuery, new { UserId = userId }, transaction);

            const string insertQuery = "INSERT INTO PasswordResetToken (user_id, token_hash) VALUES ( @UserId, @TokenHash)";
            await connection.ExecuteAsync(insertQuery, new { UserId = userId, TokenHash = token }, transaction);

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<PasswordResetToken?> GetPasswordResetTokenByEmail(string email)
    {
        User? user = await userRepository.GetUserByEmailAsync(email);
        if (user == null)
        {
            return null;
        }

        const string query = "SELECT token_id, user_id, token_hash, expiration, is_used, is_revoked, created_at FROM PasswordResetToken WHERE user_id = @UserId AND is_used = FALSE AND is_revoked = FALSE ORDER BY created_at DESC LIMIT 1";
        return await connection.QuerySingleOrDefaultAsync<PasswordResetToken>(query, new { user.UserId });
    }

    public async Task UpdatePasswordResetTokenAsync(PasswordResetToken passwordResetToken)
    {
        const string query = "UPDATE PasswordResetToken SET is_used = @IsUsed WHERE token_id = @TokenId";
        await connection.ExecuteAsync(query, passwordResetToken);
    }
}
