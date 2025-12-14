namespace TubeTracker.API.Services;

public abstract class TimedBackgroundService(TimeProvider timeProvider) : IDisposable
{
    protected readonly TimeProvider TimeProvider = timeProvider;
    private ITimer? _timer;
    protected bool Disposed;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected void InitializeTimer(TimerCallback callback, TimeSpan interval)
    {
        _timer?.Dispose();
        _timer = TimeProvider.CreateTimer(callback, null, interval, interval);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (Disposed)
        {
            return;
        }

        if (disposing)
        {
            _timer?.Dispose();
        }

        Disposed = true;
    }
}
