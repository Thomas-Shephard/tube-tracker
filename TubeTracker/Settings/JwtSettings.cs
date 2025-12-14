namespace TubeTracker.API.Settings;

public class JwtSettings
{
    public required string Secret
    {
        get;
        init
        {
            if (value.Length < 32)
            {
                throw new InvalidOperationException("Secret must be at least 32 characters long.");
            }

            field = value;
        }
    }

    public required string Issuer { get; init; }
    public required string Audience { get; init; }
}
