using System.Data;
using Dapper;
using TubeTracker.API.Models.Entities;

namespace TubeTracker.API.Repositories;

public class TokenDenyRepository(IDbConnection connection) : ITokenDenyRepository
{
    public async Task DenyTokenAsync(DeniedToken deniedToken)
    {
        await connection.ExecuteAsync("INSERT IGNORE INTO DeniedToken (jti, expires_at) VALUES (@Jti, @ExpiresAt)", deniedToken);
    }

    public async Task DeleteExpiredTokensAsync(DateTime now)
    {
        await connection.ExecuteAsync("DELETE FROM DeniedToken WHERE expires_at < @Now", new { Now = now });
    }

    public async Task<IEnumerable<DeniedToken>> GetActiveDeniedTokensAsync(DateTime now)
    {
        return await connection.QueryAsync<DeniedToken>("SELECT jti, expires_at FROM DeniedToken WHERE expires_at > @Now", new { Now = now });
    }
}
