using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TubeTracker.API.Extensions;
using TubeTracker.API.Models.Entities;
using TubeTracker.API.Repositories;
using TubeTracker.API.Services;

namespace TubeTracker.API.Controllers.Auth;

[ApiController]
[Route("api/auth/refresh")]
[Tags("Auth")]
public class RefreshController(
    IUserRepository userRepository,
    ITokenDenyService tokenDenyService,
    ITokenService tokenService,
    ILogger<RefreshController> logger) : ControllerBase
{
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Refresh()
    {
        string? jti = User.GetJti();
        if (jti is null)
        {
            logger.LogWarning("Refresh attempt with missing JTI claim.");
            return BadRequest("Token does not contain a jti claim.");
        }

        DateTime? expiration = User.GetExpirationTime();
        if (expiration is null)
        {
            logger.LogWarning("Refresh attempt with missing expiration claim for JTI: {Jti}", jti);
            return BadRequest("Token does not contain a valid expiration claim.");
        }

        int? userId = User.GetUserId();
        if (userId is null)
        {
            logger.LogWarning("Refresh attempt with missing sub claim for JTI: {Jti}", jti);
            return BadRequest("Token does not contain a sub claim.");
        }

        // Invalidate the old token
        await tokenDenyService.DenyAsync(jti, expiration.Value);

        User? user = await userRepository.GetUserByIdAsync(userId.Value);
        if (user is null)
        {
            logger.LogWarning("Refresh attempt for non-existent user ID: {UserId}", userId);
            return Unauthorized();
        }

        string tokenString = tokenService.GenerateToken(user);

        logger.LogInformation("Token refreshed successfully for user {UserId}. Old JTI {Jti} denied.", user.UserId, jti);

        return Ok(new { Token = tokenString });
    }
}
