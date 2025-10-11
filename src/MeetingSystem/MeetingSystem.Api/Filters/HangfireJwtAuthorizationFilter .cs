using Hangfire.Dashboard;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Threading.Tasks;

namespace MeetingSystem.Api.Filters;

/// <summary>
/// Asynchronous Hangfire Dashboard Authorization Filter.
/// It requires a valid JWT in the request header and checks for a specific Admin role claim.
/// </summary>
public class HangfireJwtAuthorizationFilter : IDashboardAsyncAuthorizationFilter
{
    private readonly string _adminRole;

    /// <summary>
    /// Initializes the filter with the required role name.
    /// </summary>
    /// <param name="adminRole">The role name required to access the dashboard (e.g., "Admin").</param>
    public HangfireJwtAuthorizationFilter(string adminRole = "Admin")
    {
        _adminRole = adminRole;
    }

    /// <summary>
    /// Asynchronously authorizes a dashboard request.
    /// </summary>
    /// <param name="context">The dashboard context.</param>
    /// <returns>True if the request is authorized; otherwise, false.</returns>
    public async Task<bool> AuthorizeAsync(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();

        // 1. Authenticate the request using the JWT Bearer scheme.
        var authenticateResult = await httpContext.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme).ConfigureAwait(false);

        // 2. Check if authentication was successful.
        if (!authenticateResult.Succeeded || authenticateResult.Principal == null)
        {
            return false;
        }

        var user = authenticateResult.Principal;

        // 3. Check for the required role claim.
        var hasAdminRole = user.IsInRole(_adminRole);

        return hasAdminRole;
    }
}