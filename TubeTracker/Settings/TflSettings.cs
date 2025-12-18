using System.Diagnostics.CodeAnalysis;

namespace TubeTracker.API.Settings;

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
public class TflSettings
{
    public required string AppKey { get; set; }
}
