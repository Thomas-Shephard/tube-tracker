using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using TubeTracker.API.Services;
using TubeTracker.API.Settings;

namespace TubeTracker.Tests.Services;

[TestFixture]
public class SecurityLockoutServiceTests
{
    private Mock<ILogger<SecurityLockoutService>> _loggerMock;
    private Mock<TimeProvider> _timeProviderMock;
    private SecurityLockoutSettings _settings;
    private SecurityLockoutService _service;

    [SetUp]
    public void Setup()
    {
        _loggerMock = new Mock<ILogger<SecurityLockoutService>>();
        _timeProviderMock = new Mock<TimeProvider>();
        
        // Setup default time
        _timeProviderMock.Setup(x => x.GetUtcNow()).Returns(DateTimeOffset.UtcNow);
        
        // Setup CreateTimer to return a dummy timer to avoid null reference in base class
        _timeProviderMock.Setup(x => x.CreateTimer(It.IsAny<TimerCallback>(), It.IsAny<object>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan>()))
            .Returns(new Mock<ITimer>().Object);

        _settings = new SecurityLockoutSettings
        {
            MaxFailedAttempts = 3,
            InitialLockoutDuration = TimeSpan.FromMinutes(15),
            IncrementalLockoutDuration = TimeSpan.FromMinutes(5),
            FailedAttemptResetInterval = TimeSpan.FromHours(1),
            CleanupInterval = TimeSpan.FromHours(1)
        };

        _service = new SecurityLockoutService(_settings, _timeProviderMock.Object, _loggerMock.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _service.Dispose();
    }

    [Test]
    public async Task IsLockedOut_ShouldReturnFalse_WhenNoAttempts()
    {
        bool isLocked = await _service.IsLockedOut("test-key");
        Assert.That(isLocked, Is.False);
    }

    [Test]
    public async Task IsLockedOut_ShouldReturnFalse_WhenAttemptsBelowMax()
    {
        await _service.RecordFailure("test-key");
        await _service.RecordFailure("test-key");

        bool isLocked = await _service.IsLockedOut("test-key");
        Assert.That(isLocked, Is.False);
    }

    [Test]
    public async Task IsLockedOut_ShouldReturnTrue_WhenAttemptsReachMax()
    {
        await _service.RecordFailure("test-key");
        await _service.RecordFailure("test-key");
        await _service.RecordFailure("test-key");

        bool isLocked = await _service.IsLockedOut("test-key");
        Assert.That(isLocked, Is.True);
    }

    [Test]
    public async Task IsLockedOut_ShouldReturnFalse_AfterLockoutDurationExpires()
    {
        var now = DateTimeOffset.UtcNow;
        _timeProviderMock.Setup(x => x.GetUtcNow()).Returns(now);

        // Lock the user out (3 attempts)
        for (int i = 0; i < _settings.MaxFailedAttempts; i++)
        {
            await _service.RecordFailure("test-key");
        }

        Assert.That(await _service.IsLockedOut("test-key"), Is.True, "Should be locked initially");

        // Advance time by 16 minutes (duration is 15)
        _timeProviderMock.Setup(x => x.GetUtcNow()).Returns(now.AddMinutes(16));

        Assert.That(await _service.IsLockedOut("test-key"), Is.False, "Should be unlocked after time passes");
    }

    [Test]
    public async Task ResetAttempts_ShouldClearLockout()
    {
        // Lock the user out
        for (int i = 0; i < _settings.MaxFailedAttempts; i++)
        {
            await _service.RecordFailure("test-key");
        }

        Assert.That(await _service.IsLockedOut("test-key"), Is.True);

        await _service.ResetAttempts("test-key");

        Assert.That(await _service.IsLockedOut("test-key"), Is.False);
    }
}
