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
public class RefreshController(IUserRepository userRepository, ITokenDenyService tokenDenyService, ITokenService tokenService) : ControllerBase
{
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Refresh()
    {
        string? jti = User.GetJti();
        if (jti is null)
        {
            return BadRequest("Token does not contain a jti claim.");
        }

        DateTime? expiration = User.GetExpirationTime();
        if (expiration is null)
        {
            return BadRequest("Token does not contain a valid expiration claim.");
        }

        string? email = User.GetUserEmail();
        if (email is null)
        {
            return BadRequest("Token does not contain an email claim.");
        }

        // Invalidate the old token
        await tokenDenyService.DenyAsync(jti, expiration.Value);

        User? user = await userRepository.GetUserByEmailAsync(email);
        if (user is null)
        {
            return Unauthorized();
        }

        string tokenString = tokenService.GenerateToken(user);

        return Ok(new { Token = tokenString });
    }
}
