using System.Diagnostics.CodeAnalysis;

namespace TubeTracker.API.Settings;

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
public class StatusBackgroundSettings
{
    public int RefreshIntervalMinutes { get; set; }
    public int HistoryCleanupDays { get; set; } = 30;
    public double DeduplicationThresholdMinutes => RefreshIntervalMinutes * 1.5;
}
