using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using TubeTracker.API.Models.Entities;
using TubeTracker.API.Repositories;
using TubeTracker.API.Services;
using TubeTracker.API.Settings;

namespace TubeTracker.Tests.Services;

[TestFixture]
public class TokenDenyServiceTests
{
    private Mock<ITokenDenyRepository> _repoMock;
    private Mock<IServiceScopeFactory> _scopeFactoryMock;
    private Mock<IServiceScope> _scopeMock;
    private Mock<IServiceProvider> _serviceProviderMock;
    private Mock<ILogger<TokenDenyService>> _loggerMock;
    private Mock<TimeProvider> _timeProviderMock;
    private Mock<ITimer> _timerMock;
    private TimerCallback? _capturedCallback;

    private TokenDenyService _service;
    private TokenDenySettings _settings;

    [SetUp]
    public void Setup()
    {
        _repoMock = new Mock<ITokenDenyRepository>();
        _scopeFactoryMock = new Mock<IServiceScopeFactory>();
        _scopeMock = new Mock<IServiceScope>();
        _serviceProviderMock = new Mock<IServiceProvider>();
        _loggerMock = new Mock<ILogger<TokenDenyService>>();
        _timeProviderMock = new Mock<TimeProvider>();
        _timerMock = new Mock<ITimer>();

        // Setup Dependency Injection scaffolding
        _scopeFactoryMock.Setup(x => x.CreateScope()).Returns(_scopeMock.Object);
        _scopeMock.Setup(x => x.ServiceProvider).Returns(_serviceProviderMock.Object);
        _serviceProviderMock.Setup(x => x.GetService(typeof(ITokenDenyRepository))).Returns(_repoMock.Object);
        
        // Setup TimeProvider
        _timeProviderMock.Setup(x => x.GetUtcNow()).Returns(DateTimeOffset.UtcNow);
        
        // Capture the timer callback
        _timeProviderMock.Setup(x => x.CreateTimer(
            It.IsAny<TimerCallback>(), 
            It.IsAny<object>(), 
            It.IsAny<TimeSpan>(), 
            It.IsAny<TimeSpan>()))
            .Callback<TimerCallback, object, TimeSpan, TimeSpan>((cb, state, due, period) => _capturedCallback = cb)
            .Returns(_timerMock.Object);

        _settings = new TokenDenySettings
        {
            CleanupInterval = TimeSpan.FromHours(1)
        };

        // By default, repo returns empty list for initialization
        _repoMock.Setup(r => r.GetActiveDeniedTokensAsync(It.IsAny<DateTime>()))
            .ReturnsAsync(new List<DeniedToken>());

        _service = new TokenDenyService(_settings, _timeProviderMock.Object, _scopeFactoryMock.Object, _loggerMock.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _service.Dispose();
    }

    [Test]
    public async Task DenyAsync_ShouldAddTokenToMemoryAndRepo()
    {
        string jti = "test-jti";
        DateTime expires = DateTime.UtcNow.AddHours(1);

        await _service.DenyAsync(jti, expires);

        // Check memory
        bool isDenied = await _service.IsDeniedAsync(jti);
        Assert.That(isDenied, Is.True);

        // Check repo interaction
        _repoMock.Verify(r => r.DenyTokenAsync(It.Is<DeniedToken>(t => t.Jti == jti && t.ExpiresAt == expires)), Times.Once);
    }

    [Test]
    public async Task IsDeniedAsync_ShouldReturnFalse_ForUnknownToken()
    {
        bool isDenied = await _service.IsDeniedAsync("unknown-jti");
        Assert.That(isDenied, Is.False);
    }

    [Test]
    public async Task Initialization_ShouldLoadTokensFromRepo()
    {
        // Re-setup with data for initialization
        var storedToken = new DeniedToken { Jti = "db-jti", ExpiresAt = DateTime.UtcNow.AddHours(1) };
        _repoMock.Setup(r => r.GetActiveDeniedTokensAsync(It.IsAny<DateTime>()))
            .ReturnsAsync(new List<DeniedToken> { storedToken });

        // Re-instantiate service to trigger constructor logic
        var service = new TokenDenyService(_settings, _timeProviderMock.Object, _scopeFactoryMock.Object, _loggerMock.Object);

        bool isDenied = await service.IsDeniedAsync("db-jti");
        Assert.That(isDenied, Is.True);
        
        service.Dispose();
    }

    [Test]
    public async Task CleanupExpiredTokens_ShouldRemoveExpiredTokens()
    {
        // 1. Add a token that is about to expire
        string jti = "expiring-jti";
        DateTime expires = DateTime.UtcNow.AddMinutes(10);
        await _service.DenyAsync(jti, expires);

        // 2. Advance time past expiration
        var future = DateTimeOffset.UtcNow.AddMinutes(20);
        _timeProviderMock.Setup(x => x.GetUtcNow()).Returns(future);

        // 3. Trigger cleanup
        Assert.That(_capturedCallback, Is.Not.Null, "Timer callback was not captured!");
        _capturedCallback!.Invoke(null);

        // 4. Verify memory is cleaned (IsDeniedAsync should be false)
        bool isDenied = await _service.IsDeniedAsync(jti);
        Assert.That(isDenied, Is.False, "Token should be removed from memory");

        // 5. Verify repo cleanup called
        _repoMock.Verify(r => r.DeleteExpiredTokensAsync(future.UtcDateTime), Times.Once);
    }
}
