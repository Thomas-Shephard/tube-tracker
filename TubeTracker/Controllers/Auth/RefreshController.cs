using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
        string? jti = User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
        if (jti is null)
        {
            return BadRequest("Token does not contain a JTI claim.");
        }

        string? expiresValue = User.FindFirst(JwtRegisteredClaimNames.Exp)?.Value;
        if (!long.TryParse(expiresValue, out long expiresSeconds))
        {
            return BadRequest("Token does not contain a valid EXP claim.");
        }

        string? email = User.FindFirst(JwtRegisteredClaimNames.Email)?.Value;
        if (string.IsNullOrEmpty(email))
        {
            return BadRequest("Token does not contain an Email claim.");
        }

        // Invalidate the old token
        DateTime jwtExpiryTime = DateTimeOffset.FromUnixTimeSeconds(expiresSeconds).UtcDateTime;
        await tokenDenyService.DenyAsync(jti, jwtExpiryTime);

        User? user = await userRepository.GetUserByEmailAsync(email);
        if (user is null)
        {
            return Unauthorized();
        }

        string tokenString = tokenService.GenerateToken(user);

        return Ok(new { Token = tokenString });
    }
}
