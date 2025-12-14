using System.Reflection;
using DbUp;
using DbUp.Engine;

namespace TubeTracker.API;

public static class DatabaseMigrator
{
    public static void ApplyMigrations(string connectionString)
    {
        UpgradeEngine migrator = DeployChanges.To
            .MySqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly())
            .LogToConsole()
            .Build();

        DatabaseUpgradeResult result = migrator.PerformUpgrade();

        if (!result.Successful)
        {
            throw new InvalidOperationException("Database migration failed.", result.Error);
        }
    }
}
