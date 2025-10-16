using Hangfire;

using MeetingSystem.Context;
using MeetingSystem.Model;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MeetingSystem.Business;

/// <summary>
/// Defines the contract for administrative services related to user management.
/// </summary>
public interface IAdminService
{
    /// <summary>
    /// Assigns a specified role to a user.
    /// </summary>
    /// <param name="userId">The ID of the user to whom the role will be assigned.</param>
    /// <param name="roleName">The name of the role to assign.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A tuple indicating success and a corresponding error message on failure.</returns>
    Task<(bool Success, string? ErrorMessage)> AssignRoleToUserAsync(Guid userId, string roleName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a specified role from a user.
    /// </summary>
    /// <param name="userId">The ID of the user from whom the role will be removed.</param>
    /// <param name="roleName">The name of the role to remove.</param>
    /// <param name="currentUserId">The ID of the administrator performing the action.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A tuple indicating success and a corresponding error message on failure.</returns>
    Task<(bool Success, string? ErrorMessage)> RemoveRoleFromUserAsync(Guid userId, string roleName, Guid currentUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Enqueues a background job to clean up old meetings immediately.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task TriggerMeetingCleanupJobAsync();
}

/// <summary>
/// Implements administrative services for managing user roles.
/// </summary>
public class AdminService : IAdminService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AdminService> _logger;
    private readonly IBackgroundJobClient _backgroundJobClient;

    public AdminService(IUnitOfWork unitOfWork, ILogger<AdminService> logger, IBackgroundJobClient backgroundJobClient)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _backgroundJobClient = backgroundJobClient;
    }

    /// <inheritdoc />
    public async Task<(bool Success, string? ErrorMessage)> AssignRoleToUserAsync(Guid userId, string roleName, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Users.Find(u => u.Id == userId).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (user == null)
        {
            _logger.LogWarning("User {UserId} not found.", userId);
            return (false, "User not found.");
        }

        var role = await _unitOfWork.Roles.Find(r => r.Name == roleName).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (role == null)
        {
            _logger.LogWarning("Role '{RoleName}' not found.", roleName);
            return (false, "Role not found.");
        }

        var userRoleExists = await _unitOfWork.UserRoles
            .Find(ur => ur.UserId == userId && ur.RoleId == role.Id)
            .AnyAsync(cancellationToken)
            .ConfigureAwait(false);

        if (userRoleExists)
        {
            _logger.LogInformation("User {UserId} already has role '{RoleName}'.", userId, roleName);
            return (true, null); // Idempotent: Already has the role.
        }

        _unitOfWork.UserRoles.Add(new UserRole { UserId = userId, RoleId = role.Id });
        await _unitOfWork.CompleteAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Successfully assigned role '{RoleName}' to user {UserId}.", roleName, userId);
        return (true, null);
    }

    /// <inheritdoc />
    public async Task<(bool Success, string? ErrorMessage)> RemoveRoleFromUserAsync(Guid userId, string roleName, Guid currentUserId, CancellationToken cancellationToken = default)
    {
        // Critical safety check to prevent an admin from locking themselves out.
        if (userId == currentUserId && roleName.Equals("Admin", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Admin user {AdminId} attempted to remove their own Admin role.", currentUserId);
            return (false, "Administrators cannot remove their own Admin role.");
        }

        var role = await _unitOfWork.Roles.Find(r => r.Name == roleName).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (role == null)
        {
            _logger.LogWarning("Role '{RoleName}' not found.", roleName);
            return (false, "Role not found.");
        }

        var userRole = await _unitOfWork.UserRoles
            .Find(ur => ur.UserId == userId && ur.RoleId == role.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (userRole == null)
        {
            _logger.LogInformation("User {UserId} does not have role '{RoleName}'.", userId, roleName);
            return (false, "User does not have this role.");
        }

        _unitOfWork.UserRoles.Remove(userRole);
        await _unitOfWork.CompleteAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Successfully removed role '{RoleName}' from user {UserId}.", roleName, userId);
        return (true, null);
    }

    /// <inheritdoc />
    public Task TriggerMeetingCleanupJobAsync()
    {
        _backgroundJobClient.Enqueue<IMeetingCleanupService>(service => service.CleanUpAsync(true, CancellationToken.None));
        _logger.LogInformation("Manually triggered meeting cleanup job.");
        return Task.CompletedTask;
    }
}
