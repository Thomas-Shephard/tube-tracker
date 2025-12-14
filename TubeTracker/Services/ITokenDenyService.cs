namespace TubeTracker.API.Services;

public interface ITokenDenyService
{
    Task DenyAsync(string jti, DateTime expiresAt);
    Task<bool> IsDeniedAsync(string jti);
}
