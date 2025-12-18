namespace TubeTracker.API.Models.Entities;

public class LineStatusHistory
{
    public int HistoryId { get; init; }
    public int LineId { get; init; }
    public int StatusSeverity { get; init; }
    public DateTime CheckedAt { get; init; }
}
