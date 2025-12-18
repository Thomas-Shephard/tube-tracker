using System.ComponentModel.DataAnnotations;

namespace TubeTracker.API.Models.Requests;

public class ForgotPasswordRequestModel : IEmailRequest
{
    [Required]
    [EmailAddress]
    public required string Email { get; init; }
}
