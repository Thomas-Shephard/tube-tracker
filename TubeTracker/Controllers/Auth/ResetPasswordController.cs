using System.Data;
using Microsoft.AspNetCore.Mvc;
using TubeTracker.API.Models.Entities;
using TubeTracker.API.Models.Requests;
using TubeTracker.API.Repositories;
using TubeTracker.API.Utils;

namespace TubeTracker.API.Controllers.Auth;

[ApiController]
[Route("api/auth/reset-password")]
[Tags("Auth")]
public class ResetPasswordController(IDbConnection connection, IUserRepository userRepository, IPasswordResetRepository passwordResetRepository) : ControllerBase
{
    private const string FailMessage = "Failed to reset password.";

    [HttpPost]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequestModel requestModel)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        using IDbTransaction transaction = connection.BeginTransaction();
        try
        {
            User? user = await userRepository.GetUserByEmailAsync(requestModel.Email);
            if (user == null)
            {
                return BadRequest(new { message = FailMessage });
            }

            PasswordResetToken? passwordResetToken = await passwordResetRepository.GetPasswordResetTokenByEmail(requestModel.Email);

            if (passwordResetToken is null || passwordResetToken.IsUsed || passwordResetToken.IsRevoked || passwordResetToken.Expiration <= DateTime.UtcNow
                || !PasswordUtils.VerifyPassword(requestModel.Token, passwordResetToken.TokenHash))
            {
                return BadRequest(new { message = FailMessage });
            }

            string newHashedPassword = PasswordUtils.HashPasswordWithSalt(requestModel.NewPassword);
            user.PasswordHash = newHashedPassword;
            passwordResetToken.IsUsed = true;

            await userRepository.UpdateUserAsync(user);
            await passwordResetRepository.UpdatePasswordResetTokenAsync(passwordResetToken);

            transaction.Commit();
            return Ok(new { message = "Password has been reset successfully." });
        }
        catch (Exception)
        {
            transaction.Rollback();
            return StatusCode(500, new { message = "An error occurred while resetting the password." });
        }
    }
}
