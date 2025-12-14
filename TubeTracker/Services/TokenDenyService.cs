using System.Collections.Concurrent;
using TubeTracker.API.Models.Entities;
using TubeTracker.API.Repositories;
using TubeTracker.API.Settings;

namespace TubeTracker.API.Services;

public class TokenDenyService : TimedBackgroundService, ITokenDenyService
{
    private readonly ConcurrentDictionary<string, DateTime> _denylist = new();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Task _initializationTask;

    public TokenDenyService(TokenDenySettings settings, TimeProvider timeProvider, IServiceScopeFactory scopeFactory) : base(timeProvider)
    {
        _scopeFactory = scopeFactory;

        _initializationTask = LoadDeniedTokensFromDbAsync();

        InitializeTimer(CleanupExpiredTokens, settings.CleanupInterval);
    }

    public async Task DenyAsync(string jti, DateTime expiresAt)
    {
        await _initializationTask;

        using IServiceScope scope = _scopeFactory.CreateScope();
        ITokenDenyRepository tokenDenyRepository = scope.ServiceProvider.GetRequiredService<ITokenDenyRepository>();

        DeniedToken deniedToken = new()
        {
            Jti = jti,
            ExpiresAt = expiresAt
        };

        await tokenDenyRepository.DenyTokenAsync(deniedToken);

        _denylist.TryAdd(jti, expiresAt);
    }

    public async Task<bool> IsDeniedAsync(string jti)
    {
        await _initializationTask;
        return _denylist.ContainsKey(jti);
    }

    private async void CleanupExpiredTokens(object? state)
    {
        try
        {
            DateTime now = TimeProvider.GetUtcNow().UtcDateTime;

            List<string> expiredTokensInMemory = _denylist.Where(pair => pair.Value < now).Select(pair => pair.Key).ToList();
            foreach (string expiredToken in expiredTokensInMemory)
            {
                _denylist.TryRemove(expiredToken, out _);
            }

            using IServiceScope scope = _scopeFactory.CreateScope();
            ITokenDenyRepository tokenDenyRepository = scope.ServiceProvider.GetRequiredService<ITokenDenyRepository>();
            await tokenDenyRepository.DeleteExpiredTokensAsync(now);
        }
        catch (Exception)
        {
            // Suppress the exception to prevent crashing the application, as this is an async void called by a TimerCallback
        }
    }

    private async Task LoadDeniedTokensFromDbAsync()
    {
        using IServiceScope scope = _scopeFactory.CreateScope();
        ITokenDenyRepository tokenDenyRepository = scope.ServiceProvider.GetRequiredService<ITokenDenyRepository>();

        DateTime now = TimeProvider.GetUtcNow().UtcDateTime;
        IEnumerable<DeniedToken> deniedTokens = await tokenDenyRepository.GetActiveDeniedTokensAsync(now);
        foreach (DeniedToken deniedToken in deniedTokens)
        {
            _denylist.TryAdd(deniedToken.Jti, deniedToken.ExpiresAt);
        }
    }
}
