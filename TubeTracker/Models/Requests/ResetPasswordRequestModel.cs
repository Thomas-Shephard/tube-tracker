using System.ComponentModel.DataAnnotations;
using TubeTracker.API.Attributes;

namespace TubeTracker.API.Models.Requests;

public class ResetPasswordRequestModel : IEmailRequest
{
    [Required]
    [EmailAddress]
    public required string Email { get; init; }

    [Required]
    [StringLength(6, MinimumLength = 6)]
    public required string Token { get; init; }

    [Required]
    [StrongPassword]
    public required string NewPassword { get; init; }
}
