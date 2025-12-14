using System.Diagnostics.CodeAnalysis;

namespace TubeTracker.API.Settings;

[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
public class DatabaseSettings
{
    public required string Host { get; set; }
    private static int Port => 3306;
    public required string Name { get; set; }
    public required string User { get; set; }
    public required string Password { get; set; }

    public string ConnectionString => $"Server={Host};Port={Port};Database={Name};User={User};Password={Password}";
}
