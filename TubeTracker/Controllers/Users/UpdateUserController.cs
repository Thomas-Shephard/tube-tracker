using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TubeTracker.API.Extensions;
using TubeTracker.API.Models.Entities;
using TubeTracker.API.Models.Requests;
using TubeTracker.API.Repositories;

namespace TubeTracker.API.Controllers.Users;

[ApiController]
[Route("api/user")]
[Tags("User")]
public class UpdateUserController(IUserRepository userRepository, ILogger<UpdateUserController> logger) : ControllerBase
{
    [HttpPut]
    [Authorize]
    public async Task<IActionResult> UpdateUser([FromBody] UpdateUserRequestModel requestModel)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        string? email = User.GetUserEmail();
        if (email is null)
        {
            logger.LogWarning("User update attempt with missing email claim.");
            return BadRequest("Token does not contain an email claim.");
        }

        User? user = await userRepository.GetUserByEmailAsync(email);
        if (user is null)
        {
            logger.LogWarning("User update attempt for non-existent user: {Email}", email);
            return NotFound(new { message = "User not found." });
        }

        user.Name = requestModel.Name;

        await userRepository.UpdateUserAsync(user);

        logger.LogInformation("Information updated for user {UserId}.", user.UserId);

        return Ok(new { message = "User information updated successfully." });
    }
}
