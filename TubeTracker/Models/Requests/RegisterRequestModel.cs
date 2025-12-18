using System.ComponentModel.DataAnnotations;
using TubeTracker.API.Attributes;

namespace TubeTracker.API.Models.Requests;

public class RegisterRequestModel : IEmailRequest
{
    [Required]
    [EmailAddress]
    public required string Email { get; init; }

    [Required]
    [StringLength(70, MinimumLength = 2)]
    public required string Name { get; init; }

    [Required]
    [StrongPassword]
    public required string Password { get; init; }
}
