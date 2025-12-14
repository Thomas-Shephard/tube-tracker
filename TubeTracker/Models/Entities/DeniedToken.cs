namespace TubeTracker.API.Models.Entities;

public class DeniedToken
{
    public required string Jti { get; init; }
    public DateTime ExpiresAt { get; init; }
}
