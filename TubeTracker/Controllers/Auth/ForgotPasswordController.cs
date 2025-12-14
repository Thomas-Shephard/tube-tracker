using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using TubeTracker.API.Models.Entities;
using TubeTracker.API.Models.Requests;
using TubeTracker.API.Repositories;
using TubeTracker.API.Services;
using TubeTracker.API.Utils;

namespace TubeTracker.API.Controllers.Auth;

[ApiController]
[Route("api/auth/forgot-password")]
[Tags("Auth")]
public class ForgotPasswordController(IUserRepository userRepository, IPasswordResetRepository passwordResetRepository, IEmailService emailService) : ControllerBase
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
        if (user == null)
        {
            return Ok(new { message = SuccessMessage });
        }

        // Create a cryptographically secure 6 digit random number
        string token = RandomNumberGenerator.GetInt32(100_000, 999_999).ToString("D6");

        string hashedToken = PasswordUtils.HashPasswordWithSalt(token);

        await passwordResetRepository.CreateTokenAsync(user.UserId, hashedToken);

        await emailService.SendEmailAsync(user.Email, "Reset Your TubeTracker Password", "TubeTracker Password Reset", $"Use the code {token} to reset your password.");

        return Ok(new { message = SuccessMessage });
    }
}
