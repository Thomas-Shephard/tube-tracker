using Microsoft.AspNetCore.Mvc;
using TubeTracker.API.Models.Entities;
using TubeTracker.API.Models.Requests;
using TubeTracker.API.Repositories;
using TubeTracker.API.Services;
using TubeTracker.API.Utils;

namespace TubeTracker.API.Controllers.Auth;

[ApiController]
[Route("api/auth/login")]
[Tags("Auth")]
public class LoginController(IUserRepository userRepository, ITokenService tokenService, ILogger<LoginController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Login([FromBody] LoginRequestModel requestModel)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        User? user = await userRepository.GetUserByEmailAsync(requestModel.Email);

        // To prevent timing attacks, the code path should be the same whether the user is found or not.
        // The password verification will then fail, but it will have taken the same amount of time.
        bool isPasswordCorrect = PasswordUtils.VerifyPassword(requestModel.Password, user?.PasswordHash);

        if (!isPasswordCorrect)
        {
            logger.LogWarning("Failed login attempt for email: {Email}", requestModel.Email);
            return Unauthorized();
        }

        DateTime loginTime = DateTime.UtcNow;

        user!.LastLogin = loginTime;
        await userRepository.UpdateUserAsync(user);

        string tokenString = tokenService.GenerateToken(user);

        logger.LogInformation("User {UserId} logged in successfully.", user.UserId);

        return Ok(new { Token = tokenString });
    }
}
