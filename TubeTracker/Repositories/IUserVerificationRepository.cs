using System.Data;
using TubeTracker.API.Models.Entities;

namespace TubeTracker.API.Repositories;

public interface IUserVerificationRepository
{
    Task CreateTokenAsync(int userId, string tokenHash, IDbTransaction? transaction = null);
    Task<UserVerificationToken?> GetTokenByUserIdAsync(int userId, IDbTransaction? transaction = null);
}
