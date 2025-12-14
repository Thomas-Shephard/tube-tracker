namespace TubeTracker.API.Models.Entities;

public class PasswordResetToken
{
    public int TokenId { get; init; }
    public int UserId { get; init; }
    public required string TokenHash { get; init; }
    public DateTime Expiration { get; init; }
    public bool IsUsed { get; set; }
    public bool IsRevoked { get; init; }
    public DateTime CreatedAt { get; init; }
}
