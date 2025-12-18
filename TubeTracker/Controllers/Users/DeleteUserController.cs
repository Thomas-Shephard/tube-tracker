using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TubeTracker.API.Extensions;
using TubeTracker.API.Models.Entities;
using TubeTracker.API.Repositories;
using TubeTracker.API.Services;

namespace TubeTracker.API.Controllers.Users;

[ApiController]
[Route("api/user")]
[Tags("User")]
public class DeleteUserController(IUserRepository userRepository, ITokenDenyService tokenDenyService, ILogger<DeleteUserController> logger) : ControllerBase
{
    [HttpDelete]
    [Authorize]
    public async Task<IActionResult> DeleteAccount()
    {
        string? email = User.GetUserEmail();
        if (email is null)
        {
            logger.LogWarning("Account deletion attempt with missing email claim.");
            return BadRequest("Token does not contain an email claim.");
        }

        User? user = await userRepository.GetUserByEmailAsync(email);
        if (user is null)
        {
            logger.LogWarning("Account deletion attempt for non-existent user: {Email}", email);
            return NotFound(new { message = "User not found." });
        }

        string? jti = User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
        string? expiresValue = User.FindFirst(JwtRegisteredClaimNames.Exp)?.Value;

        if (jti is not null && long.TryParse(expiresValue, out long expiresSeconds))
        {
            DateTime jwtExpiryTime = DateTimeOffset.FromUnixTimeSeconds(expiresSeconds).UtcDateTime;
            await tokenDenyService.DenyAsync(jti, jwtExpiryTime);
            logger.LogInformation("JTI {Jti} denied due to account deletion of {UserId}.", jti, user.UserId);
        }

        await userRepository.DeleteUserAsync(user.UserId);

        logger.LogInformation("Account for user {UserId} ({Email}) deleted successfully.", user.UserId, email);

        return Ok(new { message = "Account deleted successfully." });
    }
}
