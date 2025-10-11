using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Security.Claims;
using System.Text;

using FluentValidation;
using FluentValidation.AspNetCore;

using Hangfire;

using MeetingSystem.Context;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

using Minio;
using Minio.AspNetCore.HealthChecks;

namespace MeetingSystem.Api;

/// <summary>
/// Extension methods for configuring services in the Dependency Injection (DI) container.
/// This class follows the convention of grouping service registrations by feature or concern.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers Hangfire services for background job processing.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the services to.</param>
    /// <param name="configuration">The application configuration for accessing the connection string.</param>
    /// <returns>The same <see cref="IServiceCollection"/> so that multiple calls can be chained.</returns>
    public static IServiceCollection AddHangfireServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")!;

        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseSqlServerStorage(connectionString));

        services.AddHangfireServer();

        return services;
    }

    /// <summary>
    /// Registers the Minio client for S3-compatible object storage.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the services to.</param>
    /// <param name="configuration">The application configuration for accessing Minio settings.</param>
    /// <returns>The same <see cref="IServiceCollection"/> so that multiple calls can be chained.</returns>
    public static IServiceCollection AddMinioServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Note: Removing <IMinioClient> not preferred
        services.TryAddSingleton<IMinioClient>(serviceProvider =>
        {
            if (!bool.TryParse(configuration["Minio:SSL"], out bool ssl))
            {
                ssl = false;
            }

            return new MinioClient()
                .WithEndpoint(configuration["Minio:Endpoint"])
                .WithCredentials(configuration["Minio:AccessKey"], configuration["Minio:SecretKey"])
                .WithSSL(ssl)
                .Build();
        });

        return services;
    }

    /// <summary>
    /// Registers application health check services.
    /// </summary>
    /// <remarks>
    /// This includes checks for critical downstream dependencies like the SQL database and the Minio object storage.
    /// The health check endpoint is exposed at '/health'.
    /// </remarks>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the services to.</param>
    /// <param name="configuration">The application configuration for accessing connection strings.</param>
    /// <returns>The same <see cref="IServiceCollection"/> so that multiple calls can be chained.</returns>
    public static IServiceCollection AddHealthCheckServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")!;

        services.AddHealthChecks()
            .AddSqlServer(connectionString, name: "Database")
            .AddMinio(sp => sp.GetRequiredService<IMinioClient>(), name: "Object Storage")
            .AddUrlGroup(new Uri("http://mailpit:8025"), name: "Email Service (Mailpit)", timeout: TimeSpan.FromSeconds(5));

        return services;
    }

    /// <summary>
    /// Registers Cross-Origin Resource Sharing (CORS) services.
    /// </summary>
    /// <remarks>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the services to.</param>
    /// <param name="configuration">The application configuration for accessing CORS settings.</param>
    /// <returns>The same <see cref="IServiceCollection"/> so that multiple calls can be chained.</returns>
    public static IServiceCollection AddCorsServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddCors(options =>
        {
            options.AddPolicy("AllowedApps", policy =>
            {
                policy.WithOrigins(configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()!)
                      .AllowAnyHeader()
                      .AllowAnyMethod();
            });
        });

        return services;
    }

    /// <summary>
    /// Registers authentication and authorization services.
    /// </summary>
    /// <remarks>
    /// Configures JWT Bearer authentication, including token validation parameters and an event handler
    /// to check for revoked tokens (blacklist) on each validated request.
    /// </remarks>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the services to.</param>
    /// <param name="configuration">The application configuration for accessing JWT settings.</param>
    /// <returns>The same <see cref="IServiceCollection"/> so that multiple calls can be chained.</returns>
    public static IServiceCollection AddAuthServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = configuration["Jwt:Issuer"],
                    ValidAudience = configuration["Jwt:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!))
                };

                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var dbContext = context.HttpContext.RequestServices.GetRequiredService<MeetingSystemDbContext>();
                        var jti = context.Principal?.FindFirstValue(JwtRegisteredClaimNames.Jti);

                        if (string.IsNullOrEmpty(jti))
                        {
                            context.Fail("Token does not contain a JTI claim.");
                            return;
                        }

                        var tokenIsRevoked = await dbContext.RevokedTokens.AnyAsync(rt => rt.Jti == jti).ConfigureAwait(false);
                        if (tokenIsRevoked)
                        {
                            context.Fail("This token has been revoked.");
                        }
                    }
                };
            });

        services.AddAuthorization();

        return services;
    }

    /// <summary>
    /// Registers FluentValidation services for automatic model validation.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the services to.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The same <see cref="IServiceCollection"/> so that multiple calls can be chained.</returns>
    public static IServiceCollection AddValidationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddFluentValidationAutoValidation().AddFluentValidationClientsideAdapters();
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        return services;
    }

    /// <summary>
    /// Registers Swagger/OpenAPI services for API documentation and exploration.
    /// </summary>
    /// <remarks>
    /// Includes configuration to support JWT Bearer token authentication in the Swagger UI.
    /// </remarks>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the services to.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The same <see cref="IServiceCollection"/> so that multiple calls can be chained.</returns>
    public static IServiceCollection AddSwaggerServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo { Title = "MeetingSystem API", Version = "v1" });
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                In = ParameterLocation.Header,
                Description = "Please enter a valid token",
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                BearerFormat = "JWT",
                Scheme = "Bearer"
            });
            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                    },
                    Array.Empty<string>()
                }
            });
        });

        return services;
    }
}