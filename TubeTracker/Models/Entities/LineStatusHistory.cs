namespace TubeTracker.API.Models.Entities;

public class LineStatusHistory
{
    public int HistoryId { get; init; }
    public int LineId { get; init; }
    public int StatusSeverity { get; init; }
    public DateTime FirstCheckedAt { get; init; }
    public DateTime LastCheckedAt { get; init; }
    public required StatusSeverity Severity { get; init; }
}
