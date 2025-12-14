using System.ComponentModel.DataAnnotations;

namespace TubeTracker.API.Attributes;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
public class StrongPasswordAttribute : ValidationAttribute
{
    public StrongPasswordAttribute()
    {
        ErrorMessage = "Password must be at least 8 characters long and contain at least one uppercase letter, one lowercase letter, and one number.";
    }

    public override bool IsValid(object? value)
    {
        if (value is not string password)
        {
            return false;
        }

        if (password.Length < 8)
        {
            return false;
        }

        return password.Any(char.IsUpper)
               && password.Any(char.IsLower)
               && password.Any(char.IsDigit);
    }
}
