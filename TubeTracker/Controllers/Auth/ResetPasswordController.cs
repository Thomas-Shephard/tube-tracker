using System.Data;
using Microsoft.AspNetCore.Mvc;
using TubeTracker.API.Attributes;
using TubeTracker.API.Models.Entities;
using TubeTracker.API.Models.Requests;
using TubeTracker.API.Repositories;
using TubeTracker.API.Services;
using TubeTracker.API.Utils;

namespace TubeTracker.API.Controllers.Auth;

[ApiController]
[Route("api/auth/reset-password")]
[Tags("Auth")]
public class ResetPasswordController(
    IDbConnection connection,
    IUserRepository userRepository,
    IPasswordResetRepository passwordResetRepository,
    ISecurityLockoutService securityLockoutService,
    ILogger<ResetPasswordController> logger) : ControllerBase
{
    private const string FailMessage = "Failed to reset password.";

    [HttpPost]
    [SecurityLockout]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequestModel requestModel)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        using IDbTransaction transaction = connection.BeginTransaction();
        try
        {
            User? user = await userRepository.GetUserByEmailAsync(requestModel.Email, transaction);
            if (user == null)
            {
                logger.LogWarning("Password reset attempt for non-existent email: {Email}", requestModel.Email);
                return BadRequest(new { message = FailMessage });
            }

            PasswordResetToken? passwordResetToken = await passwordResetRepository.GetPasswordResetTokenByEmail(requestModel.Email, transaction);

            if (passwordResetToken is null || passwordResetToken.IsUsed || passwordResetToken.IsRevoked || passwordResetToken.Expiration <= DateTime.UtcNow
                || !PasswordUtils.VerifyPassword(requestModel.Token, passwordResetToken.TokenHash))
            {
                logger.LogWarning("Invalid or expired password reset token used for email: {Email}", requestModel.Email);
                return BadRequest(new { message = FailMessage });
            }

            string newHashedPassword = PasswordUtils.HashPasswordWithSalt(requestModel.NewPassword);
            user.PasswordHash = newHashedPassword;
            user.IsVerified = true;
            passwordResetToken.IsUsed = true;

            await userRepository.UpdateUserAsync(user, transaction);
            await passwordResetRepository.UpdatePasswordResetTokenAsync(passwordResetToken, transaction);

            transaction.Commit();

            // Reset security attempts on success
            string? ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            if (!string.IsNullOrEmpty(ipAddress))
            {
                await securityLockoutService.ResetAttempts($"IP:{ipAddress}", $"Email:{requestModel.Email}");
            }

            logger.LogInformation("Password reset successfully for user {UserId}", user.UserId);
            return Ok(new { message = "Password has been reset successfully." });
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            logger.LogError(ex, "Error occurred during password reset for email: {Email}", requestModel.Email);
            throw;
        }
    }
}
