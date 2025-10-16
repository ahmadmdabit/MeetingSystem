using MeetingSystem.Model;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Polly;

namespace MeetingSystem.Context;

/// <summary>
/// Extension methods for setting up data access services in the DI container.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers the DbContext, Unit of Work, and repositories for the data access layer.
    /// </summary>
    /// <param name="services">The IServiceCollection to add services to.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The same service collection so that multiple calls can be chained.</returns>
    public static IServiceCollection AddContextServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddDataProtection().PersistKeysToDbContext<MeetingSystemDbContext>();

        services.AddDbContext<MeetingSystemDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }

    /// <summary>
    /// Applies any pending Entity Framework migrations to the database.
    /// This method includes a retry policy to handle transient database connection errors during startup.
    /// </summary>
    /// <param name="services">The IServiceProvider to resolve services from.</param>
    /// <param name="logger">The logger for recording migration events.</param>
    public static void ApplyMigrations(
        this IServiceProvider services,
        ILogger logger)
    {
        logger.LogInformation("Applying database migrations...");

        // Define a retry policy for transient SQL exceptions during startup.
        var retryPolicy = Policy
            .Handle<SqlException>()
            .WaitAndRetry(5, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (exception, timeSpan, retryCount, context) =>
                {
                    logger.LogWarning(exception, "Retrying database connection... Attempt {RetryCount}", retryCount);
                });

        // Execute the migration logic within the retry policy.
        retryPolicy.Execute(() =>
        {
            // Resolve DbContext from a new scope to ensure proper disposal.
            using var scope = services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MeetingSystemDbContext>();

            dbContext.Database.Migrate();

            SeedRolesAsync(dbContext, logger).GetAwaiter().GetResult();

            logger.LogInformation("Database migrations applied successfully.");
        });
    }

    private static async Task SeedRolesAsync(MeetingSystemDbContext context, ILogger logger)
    {
        string[] roleNames = ["Admin", "User"];
        foreach (var roleName in roleNames)
        {
            if (!await context.Roles.AnyAsync(r => r.Name == roleName).ConfigureAwait(false))
            {
                logger.LogInformation("Seeding role: {RoleName}", roleName);
                context.Roles.Add(new Role { Id = Guid.NewGuid(), Name = roleName });
            }
        }
        await context.SaveChangesAsync().ConfigureAwait(false);
    }
}