using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using TubeTracker.API.Models.Requests;
using TubeTracker.API.Services;

namespace TubeTracker.API.Attributes;

[AttributeUsage(AttributeTargets.Method)]
public class SecurityLockoutAttribute : Attribute, IAsyncActionFilter
{
    public bool AlwaysRecord { get; set; }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        ISecurityLockoutService? securityLockoutService = context.HttpContext.RequestServices.GetService<ISecurityLockoutService>();
        ILogger<SecurityLockoutAttribute>? logger = context.HttpContext.RequestServices.GetService<ILogger<SecurityLockoutAttribute>>();

        if (securityLockoutService == null)
        {
            await next();
            return;
        }

        string? ipAddress = context.HttpContext.Connection.RemoteIpAddress?.ToString();
        if (string.IsNullOrEmpty(ipAddress))
        {
            logger?.LogWarning("SecurityLockout: Could not determine IP address for request to {Path}", context.HttpContext.Request.Path);
            context.Result = new BadRequestObjectResult("Could not determine IP address.");
            return;
        }

        string ipKey = $"IP:{ipAddress}";
        string? emailKey = null;

        IEmailRequest? emailRequest = context.ActionArguments.Values.OfType<IEmailRequest>().FirstOrDefault();
        if (emailRequest != null)
        {
            emailKey = $"Email:{emailRequest.Email}";
        }

        string[] keys = emailKey != null ? [ipKey, emailKey] : [ipKey];

        if (await securityLockoutService.IsLockedOut(keys))
        {
            logger?.LogWarning("SecurityLockout: Blocking request from {IP} (Email: {Email}) to {Path}", ipAddress, emailRequest?.Email ?? "N/A", context.HttpContext.Request.Path);
            context.Result = new ObjectResult(new { message = "Too many attempts. Please try again later." })
            {
                StatusCode = 429
            };
            return;
        }

        ActionExecutedContext executedContext = await next();

        if (AlwaysRecord || (executedContext.Result is IStatusCodeActionResult sar && sar.StatusCode >= 400 && sar.StatusCode != 429))
        {
            logger?.LogInformation("SecurityLockout: Recording failure for {IP} (Email: {Email}) on {Path}. Status: {Status}", 
                ipAddress, emailRequest?.Email ?? "N/A", context.HttpContext.Request.Path, (executedContext.Result as IStatusCodeActionResult)?.StatusCode);
            await securityLockoutService.RecordFailure(keys);
        }
    }
}
