using System.Collections.Concurrent;
using TubeTracker.API.Settings;

namespace TubeTracker.API.Services;

public class SecurityLockoutService : TimedBackgroundService, ISecurityLockoutService
{
    private readonly ConcurrentDictionary<string, (int Count, DateTime LastAttempt)> _failedAttempts = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DateTime> _lockouts = new(StringComparer.OrdinalIgnoreCase);

    private readonly SecurityLockoutSettings _settings;
    private readonly ILogger<SecurityLockoutService> _logger;

    public SecurityLockoutService(SecurityLockoutSettings settings, TimeProvider timeProvider, ILogger<SecurityLockoutService> logger) : base(timeProvider)
    {
        _settings = settings;
        _logger = logger;
        InitializeTimer(CleanupExpiredLockoutsAndStaleAttempts, _settings.CleanupInterval);
    }

    public Task<bool> IsLockedOut(params string[] keys)
    {
        DateTime now = TimeProvider.GetUtcNow().UtcDateTime;
        foreach (string key in keys)
        {
            if (_lockouts.TryGetValue(key, out DateTime expiry) && expiry > now)
            {
                _logger.LogWarning("Access denied: {Key} is locked out until {Expiry}", key, expiry);
                return Task.FromResult(true);
            }
        }
        return Task.FromResult(false);
    }

    public Task RecordFailure(params string[] keys)
    {
        foreach (string key in keys)
        {
            HandleFailedAttempt(key);
        }

        return Task.CompletedTask;
    }

    public Task ResetAttempts(params string[] keys)
    {
        foreach (string key in keys)
        {
            if (_failedAttempts.TryRemove(key, out _) | _lockouts.TryRemove(key, out _))
            {
                _logger.LogInformation("Security attempts reset for {Key}", key);
            }
        }

        return Task.CompletedTask;
    }

    private void HandleFailedAttempt(string key)
    {
        _failedAttempts.AddOrUpdate(key, (1, TimeProvider.GetUtcNow().UtcDateTime), (_, existing) =>
        {
            int newAttemptCount = existing.Count + 1;

            if (newAttemptCount < _settings.MaxFailedAttempts)
            {
                _logger.LogDebug("Recorded failure for {Key}. Attempt {Count} of {Max}", key, newAttemptCount, _settings.MaxFailedAttempts);
                return (newAttemptCount, TimeProvider.GetUtcNow().UtcDateTime);
            }

            TimeSpan lockoutDuration = _settings.InitialLockoutDuration + (newAttemptCount - _settings.MaxFailedAttempts) * _settings.IncrementalLockoutDuration;
            DateTime lockoutExpiry = TimeProvider.GetUtcNow().UtcDateTime + lockoutDuration;
            
            _lockouts.AddOrUpdate(key, lockoutExpiry, (_, _) => lockoutExpiry);
            
            _logger.LogCritical("SECURITY: {Key} has been locked out until {Expiry} due to {Count} failed attempts. (Duration: {Duration})", 
                key, lockoutExpiry, newAttemptCount, lockoutDuration);

            return (newAttemptCount, TimeProvider.GetUtcNow().UtcDateTime);
        });
    }

    private void CleanupExpiredLockoutsAndStaleAttempts(object? state)
    {
        try
        {
            DateTime now = TimeProvider.GetUtcNow().UtcDateTime;
            int lockoutCleanupCount = 0;
            int attemptCleanupCount = 0;

            foreach ((string key, _) in _lockouts.Where(lockout => lockout.Value < now).ToList())
            {
                if (_lockouts.TryRemove(key, out _))
                {
                    _failedAttempts.TryRemove(key, out _);
                    lockoutCleanupCount++;
                }
            }

            foreach ((string key, (int Count, DateTime LastAttempt) value) in _failedAttempts.ToList())
            {
                if (!_lockouts.ContainsKey(key) && now - value.LastAttempt > _settings.FailedAttemptResetInterval)
                {
                    if (_failedAttempts.TryRemove(key, out _)) attemptCleanupCount++;
                }
            }

            if (lockoutCleanupCount > 0 || attemptCleanupCount > 0)
            {
                _logger.LogInformation("SecurityLockout cleanup: Removed {LockoutCount} expired lockouts and {AttemptCount} stale attempt records.", 
                    lockoutCleanupCount, attemptCleanupCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during SecurityLockout cleanup.");
        }
    }
}
