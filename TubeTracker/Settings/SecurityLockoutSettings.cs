namespace TubeTracker.API.Settings;

public class SecurityLockoutSettings
{
    public int MaxFailedAttempts { get; init; }
    public TimeSpan InitialLockoutDuration { get; init; }
    public TimeSpan IncrementalLockoutDuration { get; init; }
    public TimeSpan FailedAttemptResetInterval { get; init; }
    public TimeSpan CleanupInterval { get; init; }
}
