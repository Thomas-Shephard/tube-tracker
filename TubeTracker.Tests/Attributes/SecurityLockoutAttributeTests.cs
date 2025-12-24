using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using TubeTracker.API.Attributes;
using TubeTracker.API.Services;

namespace TubeTracker.Tests.Attributes;

[TestFixture]
public class SecurityLockoutAttributeTests
{
    private Mock<ISecurityLockoutService> _lockoutServiceMock;
    private Mock<ILogger<SecurityLockoutAttribute>> _loggerMock;
    private Mock<IServiceProvider> _serviceProviderMock;
    private SecurityLockoutAttribute _attribute;

    [SetUp]
    public void Setup()
    {
        _lockoutServiceMock = new Mock<ISecurityLockoutService>();
        _loggerMock = new Mock<ILogger<SecurityLockoutAttribute>>();
        _serviceProviderMock = new Mock<IServiceProvider>();

        // Setup Service Provider
        _serviceProviderMock.Setup(x => x.GetService(typeof(ISecurityLockoutService)))
            .Returns(_lockoutServiceMock.Object);
        _serviceProviderMock.Setup(x => x.GetService(typeof(ILogger<SecurityLockoutAttribute>)))
            .Returns(_loggerMock.Object);

        _attribute = new SecurityLockoutAttribute();
    }

    private ActionExecutingContext CreateContext(string ipAddress)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse(ipAddress);
        httpContext.RequestServices = _serviceProviderMock.Object;

        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ActionDescriptor()
        );

        return new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object>(),
            new Mock<Controller>().Object
        );
    }

    private ActionExecutionDelegate CreateNextDelegate(int statusCode)
    {
        return () =>
        {
            var ctx = new ActionExecutedContext(
                new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor()),
                new List<IFilterMetadata>(),
                new Mock<Controller>().Object
            );
            
            // Simulating result
            if (statusCode == 200) ctx.Result = new OkResult();
            else ctx.Result = new StatusCodeResult(statusCode);
            
            return Task.FromResult(ctx);
        };
    }

    [Test]
    public async Task OnActionExecutionAsync_BlocksRequest_WhenUserIsLockedOut()
    {
        // Arrange
        var context = CreateContext("127.0.0.1");
        _lockoutServiceMock.Setup(s => s.IsLockedOut(It.IsAny<string[]>()))
            .ReturnsAsync(true);

        bool nextCalled = false;
        ActionExecutionDelegate next = () =>
        {
            nextCalled = true;
            return Task.FromResult<ActionExecutedContext>(null!);
        };

        // Act
        await _attribute.OnActionExecutionAsync(context, next);

        // Assert
        Assert.That(nextCalled, Is.False, "Next delegate should not be called when locked out");
        Assert.That(context.Result, Is.InstanceOf<ObjectResult>());
        var result = (ObjectResult)context.Result!;
        Assert.That(result.StatusCode, Is.EqualTo(429));
    }

    [Test]
    public async Task OnActionExecutionAsync_AllowsRequest_WhenUserIsNotLockedOut()
    {
        // Arrange
        var context = CreateContext("127.0.0.1");
        _lockoutServiceMock.Setup(s => s.IsLockedOut(It.IsAny<string[]>()))
            .ReturnsAsync(false);

        bool nextCalled = false;
        ActionExecutionDelegate next = () =>
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), null) { Result = new OkResult() });
        };

        // Act
        await _attribute.OnActionExecutionAsync(context, next);

        // Assert
        Assert.That(nextCalled, Is.True);
        Assert.That(context.Result, Is.Null); // Result is set by next, but context.Result itself isn't overwritten by the attribute
    }

    [Test]
    public async Task OnActionExecutionAsync_RecordsFailure_WhenActionReturnsError()
    {
        // Arrange
        var context = CreateContext("127.0.0.1");
        _lockoutServiceMock.Setup(s => s.IsLockedOut(It.IsAny<string[]>()))
            .ReturnsAsync(false);

        // Mock next to return 401 Unauthorized
        var next = CreateNextDelegate(401);

        // Act
        await _attribute.OnActionExecutionAsync(context, next);

        // Assert
        _lockoutServiceMock.Verify(s => s.RecordFailure(It.Is<string[]>(k => k.Contains("IP:127.0.0.1"))), Times.Once);
    }

    [Test]
    public async Task OnActionExecutionAsync_DoesNotRecordFailure_WhenActionReturnsSuccess()
    {
        // Arrange
        var context = CreateContext("127.0.0.1");
        _lockoutServiceMock.Setup(s => s.IsLockedOut(It.IsAny<string[]>()))
            .ReturnsAsync(false);

        // Mock next to return 200 OK
        var next = CreateNextDelegate(200);

        // Act
        await _attribute.OnActionExecutionAsync(context, next);

        // Assert
        _lockoutServiceMock.Verify(s => s.RecordFailure(It.IsAny<string[]>()), Times.Never);
    }
}
