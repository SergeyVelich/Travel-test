using Microsoft.EntityFrameworkCore;

namespace Travel.WebApi.Data;

public static class MigrationExtensions
{
    public static void RunDbInitializer<T>(this IServiceProvider serviceProvider)
        where T : DbContext, new()
    {
        using var scope = serviceProvider.CreateScope();

        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger("DbMigration");

        RunDbInitializerInternal<T>(scope.ServiceProvider, logger);
    }

    private static void RunDbInitializerInternal<T>(IServiceProvider serviceProvider, ILogger logger)
        where T : DbContext, new()
    {
        var performanceClient = serviceProvider.GetRequiredService<T>();

        var pendingMigrations = performanceClient.Database.GetPendingMigrations();
        var appliedMigrations = performanceClient.Database.GetAppliedMigrations();

        if (pendingMigrations.Any())
        {
            performanceClient.Database.Migrate();
        }
    }
}


