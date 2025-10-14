using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MeetingSystem.Business;
using MeetingSystem.Business.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MeetingSystem.Api.Controllers;

/// <summary>
/// Provides endpoints for accessing public information about users and managing user roles.
/// </summary>
[ApiController]
[Route("api/users")]
[Authorize]
[Produces("application/json")]
public class UsersController : ControllerBase
{
    private readonly IProfilePictureService _profilePictureService;
    private readonly IAdminService _adminService;
    private readonly IAuthService _authService;

    /// <summary>
    /// Initializes a new instance of the <see cref="UsersController"/> class.
    /// </summary>
    public UsersController(IProfilePictureService profilePictureService, IAdminService adminService, IAuthService authService)
    {
        _profilePictureService = profilePictureService;
        _adminService = adminService;
        _authService = authService;
    }

    private Guid GetCurrentUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    /// Gets a list of all users in the system. (Admin Only)
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A list of all users.</returns>
    [HttpGet]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(List<UserProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetUsers(CancellationToken cancellationToken)
    {
        var users = await _authService.GetAllUsersAsync(cancellationToken);
        return Ok(users);
    }

    /// <summary>
    /// Gets a secure, short-lived URL for a specific user's profile picture.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A pre-signed URL for the user's profile picture.</returns>
    [HttpGet("{userId:guid}/profile-picture")]
    [AllowAnonymous] // This endpoint can be public, as profile pictures are generally not secret.
    [ProducesResponseType(typeof(PresignedUrlDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfilePictureUrl(Guid userId, CancellationToken cancellationToken)
    {
        var url = await _profilePictureService.GetUrlAsync(userId, cancellationToken).ConfigureAwait(false);
        return string.IsNullOrEmpty(url) ? NotFound() : Ok(new PresignedUrlDto(url));
    }

    /// <summary>
    /// Assigns a role to a user. (Admin Only)
    /// </summary>
    /// <param name="userId">The ID of the user to assign the role to.</param>
    /// <param name="dto">The role to assign.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    [HttpPost("{userId:guid}/roles")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AssignRole(Guid userId, [FromBody] AssignRoleDto dto, CancellationToken cancellationToken)
    {
        var (success, errorMessage) = await _adminService.AssignRoleToUserAsync(userId, dto.RoleName, cancellationToken);
        if (!success)
        {
            return BadRequest(new { Message = errorMessage });
        }
        return Ok();
    }

    /// <summary>
    /// Removes a role from a user. (Admin Only)
    /// </summary>
    /// <param name="userId">The ID of the user to remove the role from.</param>
    /// <param name="roleName">The name of the role to remove.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    [HttpDelete("{userId:guid}/roles/{roleName}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RemoveRole(Guid userId, string roleName, CancellationToken cancellationToken)
    {
        var (success, errorMessage) = await _adminService.RemoveRoleFromUserAsync(userId, roleName, GetCurrentUserId(), cancellationToken);
        if (!success)
        {
            return BadRequest(new { Message = errorMessage });
        }
        return NoContent();
    }
}