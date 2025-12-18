namespace TubeTracker.API.Models.Entities;

public class StationStatusHistory
{
    public int HistoryId { get; init; }
    public int StationId { get; init; }
    public required string StatusDescription { get; init; }
    public DateTime FirstReportedAt { get; init; }
    public DateTime LastReportedAt { get; init; }
}
