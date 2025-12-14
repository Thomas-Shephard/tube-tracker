using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TubeTracker.API.Services;

namespace TubeTracker.API.Controllers.Auth;

[ApiController]
[Route("api/auth/logout")]
[Tags("Auth")]
public class LogoutController(ITokenDenyService tokenDenyService) : ControllerBase
{
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Logout()
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

        DateTime jwtExpiryTime = DateTimeOffset.FromUnixTimeSeconds(expiresSeconds).UtcDateTime;

        await tokenDenyService.DenyAsync(jti, jwtExpiryTime);

        return Ok();
    }
}
