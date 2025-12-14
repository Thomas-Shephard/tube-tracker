using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TubeTracker.API.Extensions;
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

        await tokenDenyService.DenyAsync(jti, expiration.Value);

        return Ok();
    }
}
