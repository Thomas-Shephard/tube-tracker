namespace TubeTracker.API.Services;

public interface ISecurityLockoutService
{
    Task<bool> IsLockedOut(params string[] keys);
    Task RecordFailure(params string[] keys);
    Task ResetAttempts(params string[] keys);
}