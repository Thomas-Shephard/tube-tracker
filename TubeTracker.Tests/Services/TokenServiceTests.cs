using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Logging;
using Moq;
using TubeTracker.API.Models.Entities;
using TubeTracker.API.Services;
using TubeTracker.API.Settings;

namespace TubeTracker.Tests.Services;

public class TokenServiceTests
{
    private Mock<ILogger<TokenService>> _loggerMock;
    private JwtSettings _settings;
    private TokenService _service;

    [SetUp]
    public void Setup()
    {
        _loggerMock = new Mock<ILogger<TokenService>>();
        _settings = new JwtSettings
        {
            Secret = "SuperSecretKey12345678901234567890",
            Issuer = "TubeTrackerIssuer",
            Audience = "TubeTrackerAudience"
        };
        _service = new TokenService(_settings, _loggerMock.Object);
    }

    [Test]
    public void GenerateToken_ReturnsValidJwt()
    {
        User user = new()
        {
            UserId = 123,
            Email = "test@example.com",
            Name = "Test User",
            PasswordHash = "hash",
            IsVerified = true
        };

        string token = _service.GenerateToken(user);

        Assert.That(token, Is.Not.Null);
        Assert.That(token, Is.Not.Empty);

        JwtSecurityTokenHandler handler = new();
        Assert.That(handler.CanReadToken(token), Is.True);
    }

    [Test]
    public void GenerateToken_ContainsCorrectClaims()
    {
        User user = new()
        {
            UserId = 456,
            Email = "claims@example.com",
            Name = "Claims User",
            PasswordHash = "hash",
            IsVerified = true
        };

        string token = _service.GenerateToken(user);
        JwtSecurityTokenHandler handler = new();
        JwtSecurityToken? jwtToken = handler.ReadJwtToken(token);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(jwtToken.Issuer, Is.EqualTo(_settings.Issuer));
            Assert.That(jwtToken.Audiences.First(), Is.EqualTo(_settings.Audience));
        }

        string? sub = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value;
        Assert.That(sub, Is.EqualTo("456"));

        string? email = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Email)?.Value;
        Assert.That(email, Is.EqualTo("claims@example.com"));

        string? isVerified = jwtToken.Claims.FirstOrDefault(c => c.Type == "is_verified")?.Value;
        Assert.That(isVerified, Is.EqualTo("true"));
        
        string? jti = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti)?.Value;
        Assert.That(jti, Is.Not.Null);
    }
}
