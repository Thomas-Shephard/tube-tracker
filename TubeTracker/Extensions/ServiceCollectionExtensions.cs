using System.Reflection;
using System.Text.RegularExpressions;

namespace TubeTracker.API.Extensions;

public static partial class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public T AddAndConfigure<T>(IConfiguration configuration, string sectionName) where T : class
        {
            T settings = configuration.GetSection(sectionName).Get<T>() ?? throw new InvalidOperationException($"{sectionName} is not configured.");
            services.AddSingleton(settings);
            return settings;
        }

        public T AddAndConfigureFromEnv<T>(IConfiguration configuration, string prefix) where T : class
        {
            T settings = Activator.CreateInstance<T>();

            foreach (PropertyInfo prop in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(prop => prop.CanWrite))
            {
                string snakeCaseName = CamelToSnakeCaseRegex().Replace(prop.Name, "$1_$2").ToUpper();
                string envKey = $"{prefix}_{snakeCaseName}";
                string? value = configuration[envKey];

                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new InvalidOperationException($"Configuration value '{envKey}' is required.");
                }

                try
                {
                    Type targetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                    object convertedValue = Convert.ChangeType(value, targetType);
                    prop.SetValue(settings, convertedValue);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Failed to convert configuration value '{envKey}' to type {prop.PropertyType.Name}.", ex);
                }
            }

            services.AddSingleton(settings);
            return settings;
        }
    }

    [GeneratedRegex("([a-z0-9])([A-Z])")]
    private static partial Regex CamelToSnakeCaseRegex();
}
