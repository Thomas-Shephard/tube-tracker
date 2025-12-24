namespace TubeTracker.API.Services;

public interface IOllamaStatusService
{
    Task WaitUntilReadyAsync(CancellationToken cancellationToken);
    void SetReady();
}
