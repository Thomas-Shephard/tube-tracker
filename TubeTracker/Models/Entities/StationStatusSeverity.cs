namespace TubeTracker.API.Models.Entities;

public class StationStatusSeverity
{
    public int SeverityId { get; init; }
    public required string Description { get; init; }
    public int Urgency { get; init; }
}
