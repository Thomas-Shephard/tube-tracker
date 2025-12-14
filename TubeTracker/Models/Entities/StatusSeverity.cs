namespace TubeTracker.API.Models.Entities;

public class StatusSeverity
{
    public int SeverityLevel { get; init; }
    public required string Description { get; set; }
    public int Urgency { get; set; }
}
