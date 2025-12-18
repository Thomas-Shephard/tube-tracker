using System.ComponentModel.DataAnnotations;
using TubeTracker.API.Attributes;

namespace TubeTracker.API.Models.Requests;

public class LoginRequestModel : IEmailRequest
{
    [Required]
    [EmailAddress]
    public required string Email { get; init; }

    [Required]
    [StrongPassword]
    public required string Password { get; init; }
}
