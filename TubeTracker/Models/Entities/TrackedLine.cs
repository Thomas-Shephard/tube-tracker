namespace TubeTracker.API.Models.Entities;

public class TrackedLine
{
    public int TrackedLineId { get; init; }
    public int UserId { get; init; }
    public int LineId { get; init; }
    public bool? Notify { get; set; }
    public int? MinUrgency { get; set; }
    public DateTime? LastNotifiedAt { get; set; }
    public DateTime CreatedAt { get; init; }
}
