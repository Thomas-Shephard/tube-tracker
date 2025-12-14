namespace TubeTracker.API.Settings;

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
