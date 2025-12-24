using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Moq;
using TubeTracker.API.Attributes;
using TubeTracker.API.Services;

namespace TubeTracker.Tests.Attributes;

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

        _serviceProviderMock.Setup(x => x.GetService(typeof(ISecurityLockoutService)))
            .Returns(_lockoutServiceMock.Object);
        _serviceProviderMock.Setup(x => x.GetService(typeof(ILogger<SecurityLockoutAttribute>)))
            .Returns(_loggerMock.Object);

        _attribute = new SecurityLockoutAttribute();
    }

    private ActionExecutingContext CreateContext(string ipAddress)
    {
        DefaultHttpContext httpContext = new()
        {
            Connection =
            {
                RemoteIpAddress = IPAddress.Parse(ipAddress)
            },
            RequestServices = _serviceProviderMock.Object
        };

        ActionContext actionContext = new(
            httpContext,
            new RouteData(),
            new ActionDescriptor()
        );

        return new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            new Mock<Controller>().Object
        );
    }

    private static ActionExecutionDelegate CreateNextDelegate(int statusCode)
    {
        return () =>
        {
            ActionExecutedContext ctx = new(
                new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor()),
                new List<IFilterMetadata>(),
                new Mock<Controller>().Object
            )
            {
                Result = statusCode == 200
                    ? new OkResult()
                    : new StatusCodeResult(statusCode)
            };

            return Task.FromResult(ctx);
        };
    }

    [Test]
    public async Task OnActionExecutionAsync_BlocksRequest_WhenUserIsLockedOut()
    {
        ActionExecutingContext context = CreateContext("127.0.0.1");
        _lockoutServiceMock.Setup(s => s.IsLockedOut(It.IsAny<string[]>()))
            .ReturnsAsync(true);

        bool nextCalled = false;

        await _attribute.OnActionExecutionAsync(context, Next);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(nextCalled, Is.False, "Next delegate should not be called when locked out");
            Assert.That(context.Result, Is.InstanceOf<ObjectResult>());
        }
        ObjectResult? result = context.Result as ObjectResult;
        Assert.That(result, Is.Not.Null);
        Assert.That(result.StatusCode, Is.EqualTo(429));
        return;

        Task<ActionExecutedContext> Next()
        {
            nextCalled = true;
            return Task.FromResult<ActionExecutedContext>(null!);
        }
    }

    [Test]
    public async Task OnActionExecutionAsync_AllowsRequest_WhenUserIsNotLockedOut()
    {
        ActionExecutingContext context = CreateContext("127.0.0.1");
        _lockoutServiceMock.Setup(s => s.IsLockedOut(It.IsAny<string[]>()))
            .ReturnsAsync(false);

        bool nextCalled = false;

        await _attribute.OnActionExecutionAsync(context, Next);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(nextCalled, Is.True);
            Assert.That(context.Result, Is.Null);
        }
        return;

        Task<ActionExecutedContext> Next()
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), null!) { Result = new OkResult() });
        }
    }

    [Test]
    public async Task OnActionExecutionAsync_RecordsFailure_WhenActionReturnsError()
    {
        ActionExecutingContext context = CreateContext("127.0.0.1");
        _lockoutServiceMock.Setup(s => s.IsLockedOut(It.IsAny<string[]>()))
            .ReturnsAsync(false);

        ActionExecutionDelegate next = CreateNextDelegate(401);

        await _attribute.OnActionExecutionAsync(context, next);

        _lockoutServiceMock.Verify(s => s.RecordFailure(It.Is<string[]>(k => ((IEnumerable<string>)k).Contains("IP:127.0.0.1"))), Times.Once);
    }

    [Test]
    public async Task OnActionExecutionAsync_DoesNotRecordFailure_WhenActionReturnsSuccess()
    {
        ActionExecutingContext context = CreateContext("127.0.0.1");
        _lockoutServiceMock.Setup(s => s.IsLockedOut(It.IsAny<string[]>()))
            .ReturnsAsync(false);

        ActionExecutionDelegate next = CreateNextDelegate(200);

        await _attribute.OnActionExecutionAsync(context, next);

        _lockoutServiceMock.Verify(s => s.RecordFailure(It.IsAny<string[]>()), Times.Never);
    }
}
