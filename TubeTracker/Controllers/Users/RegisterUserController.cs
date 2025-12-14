using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using TubeTracker.API.Models.Entities;
using TubeTracker.API.Models.Requests;
using TubeTracker.API.Repositories;
using TubeTracker.API.Services.Background;
using TubeTracker.API.Utils;

namespace TubeTracker.API.Controllers.Users;

[ApiController]
[Route("api/user/register")]
[Tags("User")]
public class RegisterUserController(IUserRepository userRepository, IUserVerificationRepository userVerificationRepository, IEmailQueue emailQueue) : ControllerBase
{
    private const string SuccessMessage = "An account has been created successfully. Check your email for a verification code.";

    [HttpPost]
    public async Task<IActionResult> Register([FromBody] RegisterRequestModel requestModel)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // Perform CPU intensive activities every time to prevent timing attacks
        string hashedPassword = PasswordUtils.HashPasswordWithSalt(requestModel.Password);
        string token = RandomNumberGenerator.GetInt32(0, 999_999).ToString("D6");
        string hashedToken = PasswordUtils.HashPasswordWithSalt(token);

        User? existingUser = await userRepository.GetUserByEmailAsync(requestModel.Email);
        if (existingUser is null)
        {
            // Hide error message to prevent user enumeration
            return Ok(new { message = SuccessMessage });
        }

        int userId = await userRepository.CreateUserAsync(requestModel.Email, requestModel.Name, hashedPassword);
        await userVerificationRepository.CreateTokenAsync(userId, hashedToken);

        await emailQueue.QueueBackgroundEmailAsync(new EmailMessage(
            To: requestModel.Email,
            Subject: "Verify Your TubeTracker Account",
            Title: "Welcome to TubeTracker!",
            Body: $"Please use the code {token} to verify your account. The code expires in 24 hours."));

        return Ok(new { message = SuccessMessage });
    }
}
