using MeetingSystem.Business.Jobs;
using MeetingSystem.Model;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MeetingSystem.Business;

/// <summary>
/// Extension methods for setting up business layer services in the Dependency Injection (DI) container.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers all the business logic services and their interfaces with the DI container.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the services to.</param>
    /// <param name="configuration">The application's configuration, available for services that need it.</param>
    /// <returns>The same <see cref="IServiceCollection"/> so that multiple calls can be chained.</returns>
    public static IServiceCollection AddBusinessServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<IAdminService, AdminService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IGenericFileService, GenericFileService>();
        services.AddScoped<IMeetingCleanupService, MeetingCleanupService>();
        services.AddScoped<IMeetingFileService, MeetingFileService>();
        services.AddScoped<IMeetingJobs, MeetingJobs>();
        services.AddScoped<IMeetingService, MeetingService>();
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddScoped<IProfilePictureService, ProfilePictureService>();

        return services;
    }
}