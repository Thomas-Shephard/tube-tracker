namespace TubeTracker.API.Models.Entities;

public class LineStatusHistory
{
    public int HistoryId { get; init; }
    public int LineId { get; init; }
    public int StatusSeverity { get; init; }
    public DateTime FirstReportedAt { get; init; }
    public DateTime LastReportedAt { get; init; }
    public required StatusSeverity Severity { get; init; }
}
