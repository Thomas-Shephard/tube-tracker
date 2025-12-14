using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TubeTracker.API.Extensions;
using TubeTracker.API.Models.Entities;
using TubeTracker.API.Repositories;
using TubeTracker.API.Services.Background;
using TubeTracker.API.Utils;

namespace TubeTracker.API.Controllers.Users;

[ApiController]
[Route("api/user/resend-verification")]
[Tags("User")]
public class ResendVerificationController(IUserRepository userRepository, IUserVerificationRepository userVerificationRepository, IEmailQueue emailQueue) : ControllerBase
{
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> ResendVerification()
    {
        string? email = User.GetUserEmail();
        if (email is null)
        {
            return BadRequest("Token does not contain an email claim.");
        }

        User? user = await userRepository.GetUserByEmailAsync(email);
        if (user is null)
        {
            return NotFound(new { message = "User not found." });
        }

        if (user.IsVerified)
        {
            return BadRequest(new { message = "Account is already verified." });
        }

        UserVerificationToken? lastToken = await userVerificationRepository.GetTokenByUserIdAsync(user.UserId);
        if (lastToken is not null && lastToken.CreatedAt > DateTime.UtcNow.AddMinutes(-5))
        {
             return BadRequest(new { message = "Please wait 5 minutes before requesting a new verification code." });
        }

        string token = RandomNumberGenerator.GetInt32(0, 999_999).ToString("D6");
        string hashedToken = PasswordUtils.HashPasswordWithSalt(token);

        await userVerificationRepository.CreateTokenAsync(user.UserId, hashedToken);

        await emailQueue.QueueBackgroundEmailAsync(new EmailMessage(
            To: user.Email,
            Subject: "Verify Your TubeTracker Account",
            Title: "Welcome to TubeTracker!",
            Body: $"Please use the code {token} to verify your account. The code expires in 24 hours."));

        return Ok(new { message = "Verification code has been re-sent to your email." });
    }
}
