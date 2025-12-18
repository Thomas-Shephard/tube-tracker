using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using TubeTracker.API.Extensions;

namespace TubeTracker.API.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequireVerifiedAccountAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        if (context.HttpContext.User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        if (!context.HttpContext.User.IsVerified())
        {
            context.Result = new ObjectResult(new { message = "Your account must be verified to perform this action." })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }
    }
}
