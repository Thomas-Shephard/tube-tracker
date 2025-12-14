namespace TubeTracker.API.Models.Entities;

public class TrackedStation
{
    public int TrackedStationId { get; init; }
    public int UserId { get; init; }
    public int StationId { get; init; }
    public bool? Notify { get; set; }
    public bool? NotifyAccessibility { get; set; }
    public int? MinUrgency { get; set; }
    public DateTime? LastNotifiedAt { get; set; }
    public DateTime CreatedAt { get; init; }
}
