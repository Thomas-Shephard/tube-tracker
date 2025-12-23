namespace TubeTracker.API.Services;

public interface IOllamaStatusService
{
    Task WaitUntilReadyAsync(CancellationToken cancellationToken);
    void SetReady();
}

public class OllamaStatusService : IOllamaStatusService
{
    private readonly TaskCompletionSource _readyTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task WaitUntilReadyAsync(CancellationToken cancellationToken)
    {
        return _readyTcs.Task.WaitAsync(cancellationToken);
    }

    public void SetReady()
    {
        _readyTcs.TrySetResult();
    }
}
