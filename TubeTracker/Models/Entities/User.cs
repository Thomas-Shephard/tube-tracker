namespace TubeTracker.API.Models.Entities;

public class User
{
    public int UserId { get; init; }
    public required string Email { get; set; }
    public required string Name { get; set; }
    public required string PasswordHash { get; set; }
    public bool IsVerified { get; set; }
    public DateTime? LastLogin { get; set; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}
