using TubeTracker.API.Utils;

namespace TubeTracker.Tests.Utils;

public class PasswordUtilsTests
{
    [Test]
    public void VerifyPassword_CorrectPassword_ReturnsTrue()
    {
        string saltAndHash = PasswordUtils.HashPasswordWithSalt("password");

        bool result = PasswordUtils.VerifyPassword("password", saltAndHash);

        Assert.That(result, Is.True);
    }

    [Test]
    public void VerifyPassword_IncorrectPassword_ReturnsFalse()
    {
        string saltAndHash = PasswordUtils.HashPasswordWithSalt("password");

        bool result = PasswordUtils.VerifyPassword("wrong-password", saltAndHash);

        Assert.That(result, Is.False);
    }

    [Test]
    public void VerifyPassword_NoHash_ReturnsFalse()
    {
        bool result = PasswordUtils.VerifyPassword("password", null);

        Assert.That(result, Is.False);
    }

    [Test]
    public void VerifyPassword_InvalidSaltAndHash_ThrowsArgumentException()
    {
        const string invalidSaltAndHash = "6t";

        Assert.Throws<ArgumentException>(() => PasswordUtils.VerifyPassword("password", invalidSaltAndHash));
    }

    [Test]
    public void VerifyPassword_SaltAndHashTooShort_ThrowsArgumentException()
    {
        const string tooShortSaltAndHash = "RKoax+i6SXNv2Q==";

        Assert.Throws<ArgumentException>(() => PasswordUtils.VerifyPassword("password", tooShortSaltAndHash));
    }

    [Test]
    public void VerifyPassword_SaltAndHashTooLong_ThrowsArgumentException()
    {
        const string tooLongSaltAndHash = "6tCHIb4wTBIF0F+KoXIGqlDThhyI8WH2gen7O9AlXVMNcdI+HkUy2BlRI8I7g2IlXkROSKpQlZzqrBes";

        Assert.Throws<ArgumentException>(() => PasswordUtils.VerifyPassword("password", tooLongSaltAndHash));
    }
}
