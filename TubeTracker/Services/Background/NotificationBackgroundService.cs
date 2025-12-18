using System.Text;
using TubeTracker.API.Models.Notifications;
using TubeTracker.API.Repositories;

namespace TubeTracker.API.Services.Background;

public class NotificationBackgroundService(IServiceScopeFactory serviceScopeFactory, TimeProvider timeProvider) : BackgroundService
{
    private readonly TimeSpan _period = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(_period, timeProvider);

        do
        {
            await ProcessNotificationsAsync();
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task ProcessNotificationsAsync()
    {
        using IServiceScope scope = serviceScopeFactory.CreateScope();
        ITrackedLineRepository lineRepository = scope.ServiceProvider.GetRequiredService<ITrackedLineRepository>();
        ITrackedStationRepository stationRepository = scope.ServiceProvider.GetRequiredService<ITrackedStationRepository>();
        IEmailQueue emailQueue = scope.ServiceProvider.GetRequiredService<IEmailQueue>();

        try
        {
            List<LineNotificationModel> pendingLines = (await lineRepository.GetPendingNotificationsAsync()).ToList();
            List<StationNotificationModel> pendingStations = (await stationRepository.GetPendingNotificationsAsync()).ToList();

            if (pendingLines.Count == 0 && pendingStations.Count == 0)
            {
                return;
            }

            // Group by UserEmail to send consolidated emails
            IEnumerable<string> userEmails = pendingLines.Select(n => n.UserEmail)
                                                         .Concat(pendingStations.Select(n => n.UserEmail))
                                                         .Distinct();

            foreach (string email in userEmails)
            {
                List<LineNotificationModel> userLines = pendingLines.Where(n => n.UserEmail == email).ToList();
                List<StationNotificationModel> userStations = pendingStations.Where(n => n.UserEmail == email).ToList();

                string userName = userLines.FirstOrDefault()?.UserName ?? userStations.First().UserName;

                StringBuilder body = new();
                body.Append($"Hi {userName},<br/><br/>");
                body.Append("There are new disruptions on your tracked lines and stations:<br/><br/>");

                if (userLines.Count > 0)
                {
                    body.Append("<b>Lines:</b><br/><ul>");
                    foreach (LineNotificationModel n in userLines)
                    {
                        body.Append($"<li><b>{n.LineName}</b>: {n.StatusDescription} (Reported: {n.ReportedAt:f})</li>");
                    }
                    body.Append("</ul><br/>");
                }

                if (userStations.Count > 0)
                {
                    body.Append("<b>Stations:</b><br/><ul>");
                    foreach (StationNotificationModel n in userStations)
                    {
                        body.Append($"<li><b>{n.StationName}</b>: {n.StatusDescription} (Reported: {n.ReportedAt:f})</li>");
                    }
                    body.Append("</ul><br/>");
                }

                await emailQueue.QueueBackgroundEmailAsync(new EmailMessage(
                    email,
                    "TubeTracker: New Disruption Alerts",
                    "New Alerts",
                    body.ToString()
                ));

                // Update last notified status for lines and stations
                DateTime now = timeProvider.GetUtcNow().UtcDateTime;

                foreach (IGrouping<int, LineNotificationModel> group in userLines.GroupBy(n => n.TrackedLineId))
                {
                    int maxHistoryId = group.Max(n => n.HistoryId);
                    await lineRepository.UpdateLastNotifiedAsync(group.Key, maxHistoryId, now);
                }

                foreach (IGrouping<int, StationNotificationModel> group in userStations.GroupBy(n => n.TrackedStationId))
                {
                    int maxHistoryId = group.Max(n => n.HistoryId);
                    await stationRepository.UpdateLastNotifiedAsync(group.Key, maxHistoryId, now);
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to process notifications: {e.Message}");
        }
    }
}
