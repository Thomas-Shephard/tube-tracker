using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using TubeTracker.API.Models.Entities;
using TubeTracker.API.Settings;

namespace TubeTracker.API.Services;

public class TokenService(JwtSettings jwtSettings) : ITokenService
{
    public string GenerateToken(User user)
    {
        SymmetricSecurityKey securityKey = new(Encoding.UTF8.GetBytes(jwtSettings.Secret));
        SigningCredentials credentials = new(securityKey, SecurityAlgorithms.HmacSha256);

        Claim[] claims =
        [
            new(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Name, user.Name),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        ];

        DateTime now = DateTime.UtcNow;
        DateTime expires = now.AddDays(7);
        JwtSecurityToken token = new( jwtSettings.Issuer, jwtSettings.Audience, claims, now, expires, credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
