using Hangfire;
using HealthChecks.UI.Client;
using MeetingSystem.Api;
using MeetingSystem.Api.Filters;
using MeetingSystem.Business;
using MeetingSystem.Context;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Serilog;

// ..........................................................................................
// 1. Configure Bootstrap Logging
// ..........................................................................................
// This initial logger captures any errors that occur during the application host build process.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

Log.Information("Starting up MeetingSystem API");

try
{
    var builder = WebApplication.CreateBuilder(args);

    // ..........................................................................................
    // 2. Configure Host-level Services (e.g., Logging)
    // ..........................................................................................
    // Replace the default logging providers with Serilog for structured, configurable logging.
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console());

    // ..........................................................................................
    // 3. Configure Application Services in the DI Container
    // ..........................................................................................
    // Using extension methods from the MeetingSystem.Api namespace to keep this file clean.
    builder.Services.AddConfigurationServices(builder.Configuration);
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddHangfireServices(builder.Configuration);
    builder.Services.AddMinioServices(builder.Configuration);
    builder.Services.AddContextServices(builder.Configuration);
    builder.Services.AddBusinessServices(builder.Configuration);
    builder.Services.AddHealthCheckServices(builder.Configuration);
    builder.Services.AddCorsServices(builder.Configuration);
    builder.Services.AddAuthServices(builder.Configuration);
    builder.Services.AddValidationServices(builder.Configuration);
    builder.Services.AddControllers();
    builder.Services.AddSwaggerServices(builder.Configuration);

    // ..........................................................................................
    // 4. Build the Application Host
    // ..........................................................................................
    var app = builder.Build();

    // ..........................................................................................
    // 5. Apply Database Migrations on Startup
    // ..........................................................................................
    // This ensures the database is created and up-to-date before the application starts accepting requests.
    // It's a common pattern for development and containerized environments.
    try
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        app.Services.ApplyMigrations(logger);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "An error occurred during database migration. The application will shut down.");
        // Fail fast if migrations can't be applied.
        return;
    }

    // ..........................................................................................
    // 6. Configure the HTTP Request Pipeline (Middleware)
    // ..........................................................................................
    // The order of middleware registration is critical for security and functionality.
    app.UseExceptionHandler("/error");

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    // Secure the Hangfire dashboard, allowing access only to authenticated users with the "Admin" role.
    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        AsyncAuthorization = [new HangfireJwtAuthorizationFilter("Admin")]
    });

    app.UseHttpsRedirection();
    app.UseSerilogRequestLogging(); // Log every incoming HTTP request.
    app.UseCors("AllowedApps"); // Apply the configured CORS policy.

    app.UseAuthentication(); // 1. Determines who the user is.
    app.UseAuthorization();  // 2. Determines what the user is allowed to do.

    app.MapControllers();
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    }); // Expose the health check endpoint.

    // ..........................................................................................
    // 7. Run the Application
    // ..........................................................................................
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}