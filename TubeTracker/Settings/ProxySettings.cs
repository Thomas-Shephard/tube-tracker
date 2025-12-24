using System.Diagnostics.CodeAnalysis;

namespace TubeTracker.API.Settings;

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
public class ProxySettings
{
    public required string TrustedProxies { get; set; }
}
