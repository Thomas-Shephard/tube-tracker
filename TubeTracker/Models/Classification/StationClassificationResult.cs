namespace TubeTracker.API.Models.Classification;

public class StationClassificationResult
{
    public int CategoryId { get; init; }
    public bool IsFuture { get; init; }
    public DateTime? ValidFrom { get; init; }
    public DateTime? ValidUntil { get; init; }
}
