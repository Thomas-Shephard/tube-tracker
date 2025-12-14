using TubeTracker.API.Models.Entities;

namespace TubeTracker.API.Repositories;

public interface IPasswordResetRepository
{
    Task CreateTokenAsync(int userId, string token);
    Task<PasswordResetToken?> GetPasswordResetTokenByEmail(string email);
    Task UpdatePasswordResetTokenAsync(PasswordResetToken passwordResetToken);
}
