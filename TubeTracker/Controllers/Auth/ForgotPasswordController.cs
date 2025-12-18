using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using TubeTracker.API.Models.Entities;
using TubeTracker.API.Models.Requests;
using TubeTracker.API.Repositories;
using TubeTracker.API.Services.Background;
using TubeTracker.API.Utils;

namespace TubeTracker.API.Controllers.Auth;

[ApiController]
[Route("api/auth/forgot-password")]
[Tags("Auth")]
public class ForgotPasswordController(
    IUserRepository userRepository,
    IPasswordResetRepository passwordResetRepository,
    IEmailQueue emailQueue,
    ILogger<ForgotPasswordController> logger) : ControllerBase
{
    private const string SuccessMessage = "If the email exists, a password reset token has been sent.";

    [HttpPost]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequestModel requestModel)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        User? user = await userRepository.GetUserByEmailAsync(requestModel.Email);

        // Always perform token generation and hashing to mitigate timing attacks (CPU bound)
        string token = RandomNumberGenerator.GetInt32(0, 999_999).ToString("D6");
        string hashedToken = PasswordUtils.HashPasswordWithSalt(token);

        if (user is null)
        {
            logger.LogInformation("Password reset requested for non-existent email: {Email}", requestModel.Email);
            return Ok(new { message = SuccessMessage });
        }

        await passwordResetRepository.CreateTokenAsync(user.UserId, hashedToken);

        logger.LogInformation("Password reset token generated for user {UserId}", user.UserId);

        await emailQueue.QueueBackgroundEmailAsync(new EmailMessage(
            To: user.Email,
            Subject: "Reset Your TubeTracker Password",
            Title: "TubeTracker Password Reset",
            Body: $"Use the code {token} to reset your password. You have 15 minutes until the code expires."));

        return Ok(new { message = SuccessMessage });
    }
}
