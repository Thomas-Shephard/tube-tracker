using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using TubeTracker.API.Models.Entities;
using TubeTracker.API.Services;
using TubeTracker.API.Settings;

namespace TubeTracker.Tests.Services;

[TestFixture]
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
            Secret = "SuperSecretKey12345678901234567890", // Must be >= 32 bytes for HMACSHA256 usually, or at least long enough
            Issuer = "TubeTrackerIssuer",
            Audience = "TubeTrackerAudience"
        };
        _service = new TokenService(_settings, _loggerMock.Object);
    }

    [Test]
    public void GenerateToken_ReturnsValidJwt()
    {
        var user = new User
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

        var handler = new JwtSecurityTokenHandler();
        Assert.That(handler.CanReadToken(token), Is.True);
    }

    [Test]
    public void GenerateToken_ContainsCorrectClaims()
    {
        var user = new User
        {
            UserId = 456,
            Email = "claims@example.com",
            Name = "Claims User",
            PasswordHash = "hash",
            IsVerified = true
        };

        string token = _service.GenerateToken(user);
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        Assert.That(jwtToken.Issuer, Is.EqualTo(_settings.Issuer));
        Assert.That(jwtToken.Audiences.First(), Is.EqualTo(_settings.Audience));

        var sub = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value;
        Assert.That(sub, Is.EqualTo("456"));

        var email = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Email)?.Value;
        Assert.That(email, Is.EqualTo("claims@example.com"));

        var isVerified = jwtToken.Claims.FirstOrDefault(c => c.Type == "is_verified")?.Value;
        Assert.That(isVerified, Is.EqualTo("true"));
        
        var jti = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti)?.Value;
        Assert.That(jti, Is.Not.Null);
    }
}
