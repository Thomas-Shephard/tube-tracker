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
public class DeleteUserController(IUserRepository userRepository, ITokenDenyService tokenDenyService) : ControllerBase
{
    [HttpDelete]
    [Authorize]
    public async Task<IActionResult> DeleteAccount()
    {
        string? email = User.GetUserEmail();
        if (email is null)
        {
            return BadRequest("Token does not contain an email claim.");
        }

        User? user = await userRepository.GetUserByEmailAsync(email);
        if (user is null)
        {
            return NotFound(new { message = "User not found." });
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
