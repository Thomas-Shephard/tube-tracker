namespace TubeTracker.API.Models.Notifications;

public class LineNotificationModel
{
    public int TrackedLineId { get; init; }
    public int HistoryId { get; init; }
    public required string UserEmail { get; init; }
    public required string UserName { get; init; }
    public required string LineName { get; init; }
    public required string StatusDescription { get; init; }
    public DateTime ReportedAt { get; init; }
}
