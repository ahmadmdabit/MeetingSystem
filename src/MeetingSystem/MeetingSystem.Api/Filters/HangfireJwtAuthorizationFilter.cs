using Hangfire.Dashboard;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace MeetingSystem.Api.Filters;

/// <summary>
/// Synchronous Hangfire Dashboard Authorization Filter.
/// Relies on the standard ASP.NET Core authentication middleware having already run and populated the HttpContext.User.
/// </summary>
public class HangfireJwtAuthorizationFilter : IDashboardAuthorizationFilter
{
    private readonly string _role;

    /// <summary>
    /// Initializes the filter with the required role name.
    /// </summary>
    /// <param name="role">The role name required to access the dashboard (e.g., "Admin").</param>
    public HangfireJwtAuthorizationFilter(string role = "Admin")
    {
        _role = role;
    }

    /// <summary>
    /// Synchronously authorizes a dashboard request.
    /// </summary>
    /// <param name="context">The dashboard context.</param>
    /// <returns>True if the request is authorized; otherwise, false.</returns>
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        var logger = httpContext.RequestServices.GetRequiredService<ILogger<HangfireJwtAuthorizationFilter>>();
        var user = httpContext.User;

        // 1. Check if the user is authenticated at all.
        if (user.Identity?.IsAuthenticated != true)
        {
            logger.LogWarning("Hangfire dashboard authorization failed: User is not authenticated.");
            return false;
        }

        // 2. Check for the required role claim.
        if (!user.IsInRole(_role))
        {
            logger.LogWarning("Hangfire dashboard authorization failed for user {UserId}: User is not in the required role '{RequiredRole}'.", user.FindFirstValue(ClaimTypes.NameIdentifier), _role);
            return false;
        }

        logger.LogInformation("Hangfire dashboard authorization succeeded for user {UserId}.", user.FindFirstValue(ClaimTypes.NameIdentifier));
        return true;
    }
}