using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace TubeTracker.API.Extensions;

public static class ClaimsExtensions
{
    extension(ClaimsPrincipal user)
    {
        public int? GetUserId()
        {
            string? userIdString = user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            return int.TryParse(userIdString, out int userId)
                ? userId
                : null;
        }

        public string? GetUserEmail()
        {
            return user.FindFirst(JwtRegisteredClaimNames.Email)?.Value;
        }

        public bool IsVerified()
        {
            string? verifiedClaim = user.FindFirst("is_verified")?.Value;
            return bool.TryParse(verifiedClaim, out bool isVerified) && isVerified;
        }

        public DateTime? GetExpirationTime()
        {
            string? expString = user.FindFirst(JwtRegisteredClaimNames.Exp)?.Value;
            if (!long.TryParse(expString, out long expUnixTime))
                return null;
            DateTimeOffset dateTimeOffset = DateTimeOffset.FromUnixTimeSeconds(expUnixTime);
            return dateTimeOffset.UtcDateTime;
        }

        public string? GetJti()
        {
            return user.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
        }
    }
}
