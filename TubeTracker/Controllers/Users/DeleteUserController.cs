using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TubeTracker.API.Models.Entities;
using TubeTracker.API.Repositories;
using TubeTracker.API.Services;

namespace TubeTracker.API.Controllers.Users;

[ApiController]
[Route("api/user")]
[Tags("User")]
[Authorize]
public class DeleteUserController(IUserRepository userRepository, ITokenDenyService tokenDenyService) : ControllerBase
{
    [HttpDelete]
    public async Task<IActionResult> DeleteAccount()
    {
        string? email = User.FindFirst(JwtRegisteredClaimNames.Email)?.Value;
        if (string.IsNullOrEmpty(email))
        {
            return Unauthorized();
        }

        User? user = await userRepository.GetUserByEmailAsync(email);
        if (user is null)
        {
            return NotFound();
        }

        string? jti = User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
        string? expiresValue = User.FindFirst(JwtRegisteredClaimNames.Exp)?.Value;

        if (jti is not null && long.TryParse(expiresValue, out long expiresSeconds))
        {
            DateTime jwtExpiryTime = DateTimeOffset.FromUnixTimeSeconds(expiresSeconds).UtcDateTime;
            await tokenDenyService.DenyAsync(jti, jwtExpiryTime);
        }

        await userRepository.DeleteUserAsync(user.UserId);

        return Ok(new { message = "Account deleted successfully." });
    }
}
