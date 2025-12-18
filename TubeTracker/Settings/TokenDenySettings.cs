using System.Diagnostics.CodeAnalysis;

namespace TubeTracker.API.Settings;

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
public class TokenDenySettings
{
    public required TimeSpan CleanupInterval
    {
        get;
        init
        {
            if (value <= TimeSpan.Zero)
            {
                throw new InvalidOperationException($"{nameof(CleanupInterval)} must be a positive TimeSpan.");
            }

            field = value;
        }
    }
}
