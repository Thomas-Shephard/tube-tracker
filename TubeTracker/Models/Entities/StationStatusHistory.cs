namespace TubeTracker.API.Models.Entities;

public class StationStatusHistory
{
    public int HistoryId { get; init; }
    public int StationId { get; init; }
    public required string StatusDescription { get; init; }
    public DateTime FirstCheckedAt { get; init; }
    public DateTime LastCheckedAt { get; init; }
}
