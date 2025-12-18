using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TubeTracker.API.Extensions;
using TubeTracker.API.Models.Entities;
using TubeTracker.API.Repositories;
using TubeTracker.API.Utils;

namespace TubeTracker.API.Controllers.Users;

[ApiController]
[Route("api/user/verify")]
[Tags("User")]
public class VerifyUserController( IUserRepository userRepository, IUserVerificationRepository userVerificationRepository, ILogger<VerifyUserController> logger) : ControllerBase
{
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Verify([FromBody] string code)
    {
        if (string.IsNullOrEmpty(code) || code.Length != 6)
        {
            return BadRequest(new { message = "A 6 digit verification code is required." });
        }

        int? userId = User.GetUserId();
        if (userId is null)
        {
            logger.LogWarning("Verification attempt with missing userId in token.");
            return Unauthorized("Token does not contain a sub claim.");
        }

        User? user = await userRepository.GetUserByIdAsync(userId.Value);
        if (user is null)
        {
            logger.LogWarning("Verification attempt for non-existent userId: {UserId}", userId);
            return NotFound(new { message = "User not found." });
        }

        if (user.IsVerified)
        {
            logger.LogInformation("Verification attempt for already verified user {UserId}", user.UserId);
            return Ok(new { message = "Account is already verified." });
        }

        UserVerificationToken? token = await userVerificationRepository.GetTokenByUserIdAsync(user.UserId);
        if (token is null || token.ExpiresAt <= DateTime.UtcNow
                          || !PasswordUtils.VerifyPassword(code, token.TokenHash))
        {
            logger.LogWarning("Invalid or expired verification code provided for user {UserId}", user.UserId);
            return BadRequest(new { message = "Invalid or expired verification code." });
        }

        user.IsVerified = true;
        await userRepository.UpdateUserAsync(user);

        logger.LogInformation("User {UserId} verified their account successfully.", user.UserId);

        return Ok(new { message = "Account verified successfully." });
    }
}
