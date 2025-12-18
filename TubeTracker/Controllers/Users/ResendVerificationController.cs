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
public class ResendVerificationController(IUserRepository userRepository, IUserVerificationRepository userVerificationRepository, IEmailQueue emailQueue, ILogger<ResendVerificationController> logger) : ControllerBase
{
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> ResendVerification()
    {
        string? email = User.GetUserEmail();
        if (email is null)
        {
            logger.LogWarning("Resend verification attempt with missing email claim.");
            return BadRequest("Token does not contain an email claim.");
        }

        User? user = await userRepository.GetUserByEmailAsync(email);
        if (user is null)
        {
            logger.LogWarning("Resend verification attempt for non-existent user: {Email}", email);
            return NotFound(new { message = "User not found." });
        }

        if (user.IsVerified)
        {
            logger.LogInformation("Resend verification attempt for already verified user {UserId}", user.UserId);
            return BadRequest(new { message = "Account is already verified." });
        }

        UserVerificationToken? lastToken = await userVerificationRepository.GetTokenByUserIdAsync(user.UserId);
        if (lastToken is not null && lastToken.CreatedAt > DateTime.UtcNow.AddMinutes(-5))
        {
            logger.LogInformation("Resend verification rate limited for user {UserId}", user.UserId);
            return BadRequest(new { message = "Please wait 5 minutes before requesting a new verification code." });
        }

        string token = RandomNumberGenerator.GetInt32(0, 999_999).ToString("D6");
        string hashedToken = PasswordUtils.HashPasswordWithSalt(token);

        await userVerificationRepository.CreateTokenAsync(user.UserId, hashedToken);

        logger.LogInformation("New verification token generated and queued for user {UserId}", user.UserId);

        await emailQueue.QueueBackgroundEmailAsync(new EmailMessage(
            To: user.Email,
            Subject: "Verify Your TubeTracker Account",
            Title: "Welcome to TubeTracker!",
            Body: $"Please use the code {token} to verify your account. The code expires in 24 hours."));

        return Ok(new { message = "Verification code has been re-sent to your email." });
    }
}
