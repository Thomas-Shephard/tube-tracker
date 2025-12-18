using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using TubeTracker.API.Models.Entities;
using TubeTracker.API.Repositories;
using TubeTracker.API.Settings;

namespace TubeTracker.API.Services;

public class TokenDenyService : TimedBackgroundService, ITokenDenyService
{
    private readonly ConcurrentDictionary<string, DateTime> _denylist = new();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TokenDenyService> _logger;
    private readonly Task _initializationTask;

    public TokenDenyService(TokenDenySettings settings, TimeProvider timeProvider, IServiceScopeFactory scopeFactory, ILogger<TokenDenyService> logger) : base(timeProvider)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

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
        _logger.LogInformation("Token {Jti} denied until {ExpiresAt}", jti, expiresAt);
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

            if (expiredTokensInMemory.Count > 0)
            {
                _logger.LogInformation("Cleaned up {Count} expired tokens from memory and database", expiredTokensInMemory.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during token cleanup");
        }
    }

    private async Task LoadDeniedTokensFromDbAsync()
    {
        try
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            ITokenDenyRepository tokenDenyRepository = scope.ServiceProvider.GetRequiredService<ITokenDenyRepository>();

            DateTime now = TimeProvider.GetUtcNow().UtcDateTime;
            IEnumerable<DeniedToken> deniedTokens = await tokenDenyRepository.GetActiveDeniedTokensAsync(now);
            int count = 0;
            foreach (DeniedToken deniedToken in deniedTokens)
            {
                _denylist.TryAdd(deniedToken.Jti, deniedToken.ExpiresAt);
                count++;
            }
            _logger.LogInformation("Loaded {Count} active denied tokens from database", count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load denied tokens from database");
        }
    }
}
