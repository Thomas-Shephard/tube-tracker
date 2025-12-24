using NUnit.Framework;
using TubeTracker.API.Utils;

namespace TubeTracker.Tests.Utils;

[TestFixture]
public class PasswordUtilsTests
{
    [Test]
    public void HashPasswordWithSalt_ReturnsValidBase64String()
    {
        string password = "MySecurePassword123!";
        string hash = PasswordUtils.HashPasswordWithSalt(password);

        Assert.That(hash, Is.Not.Null);
        Assert.That(hash, Is.Not.Empty);
        Assert.DoesNotThrow(() => Convert.FromBase64String(hash));
    }

    [Test]
    public void HashPasswordWithSalt_ReturnsDifferentHashesForSamePassword()
    {
        string password = "Password";
        string hash1 = PasswordUtils.HashPasswordWithSalt(password);
        string hash2 = PasswordUtils.HashPasswordWithSalt(password);

        Assert.That(hash1, Is.Not.EqualTo(hash2), "Hashes should differ due to random salts");
    }

    [Test]
    public void VerifyPassword_ReturnsTrue_ForCorrectPassword()
    {
        string password = "CorrectPassword";
        string hash = PasswordUtils.HashPasswordWithSalt(password);

        bool result = PasswordUtils.VerifyPassword(password, hash);

        Assert.That(result, Is.True);
    }

    [Test]
    public void VerifyPassword_ReturnsFalse_ForIncorrectPassword()
    {
        string password = "CorrectPassword";
        string hash = PasswordUtils.HashPasswordWithSalt(password);

        bool result = PasswordUtils.VerifyPassword("WrongPassword", hash);

        Assert.That(result, Is.False);
    }

    [Test]
    public void VerifyPassword_ReturnsFalse_WhenHashIsNull()
    {
        // This tests the timing attack protection path
        bool result = PasswordUtils.VerifyPassword("Password", null);

        Assert.That(result, Is.False);
    }

    [Test]
    public void VerifyPassword_ThrowsArgumentException_WhenHashIsInvalidBase64()
    {
        Assert.Throws<ArgumentException>(() => PasswordUtils.VerifyPassword("Password", "NotBase64!!"));
    }

    [Test]
    public void VerifyPassword_ThrowsArgumentException_WhenHashIsInvalidLength()
    {
        // Valid Base64 but decodes to wrong length
        string shortHash = Convert.ToBase64String(new byte[10]); 
        
        Assert.Throws<ArgumentException>(() => PasswordUtils.VerifyPassword("Password", shortHash));
    }
}
