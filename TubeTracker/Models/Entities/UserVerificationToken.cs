namespace TubeTracker.API.Models.Entities;

public class UserVerificationToken
{
    public int TokenId { get; init; }
    public int UserId { get; init; }
    public required string TokenHash { get; init; }
    public DateTime ExpiresAt { get; init; }
    public DateTime CreatedAt { get; init; }
}
