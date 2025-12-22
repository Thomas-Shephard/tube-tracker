using TubeTracker.API.Models.Classification;

namespace TubeTracker.API.Services;

public interface ITflClassificationService
{
    Task<StationClassificationResult> ClassifyStationDisruptionAsync(string description);
}
