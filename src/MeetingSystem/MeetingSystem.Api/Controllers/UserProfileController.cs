using System.Security.Claims;

using MeetingSystem.Api.Filters;
using MeetingSystem.Business;
using MeetingSystem.Business.Dtos;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MeetingSystem.Api.Controllers;

/// <summary>
/// Manages the authenticated user's own profile.
/// </summary>
[ApiController]
[Route("api/users/me")]
[Authorize]
[Produces("application/json")]
public class UserProfileController : ControllerBase
{
    private readonly IProfilePictureService _profilePictureService;
    private readonly IAuthService _authService;
    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    /// Initializes a new instance of the <see cref="UserProfileController"/> class.
    /// </summary>
    public UserProfileController(IProfilePictureService profilePictureService, IAuthService authService)
    {
        _profilePictureService = profilePictureService;
        _authService = authService;
    }

    /// <summary>
    /// Gets the profile information for the currently authenticated user.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCurrentUserProfile(CancellationToken cancellationToken)
    {
        var userProfile = await _authService.GetCurrentUserProfileAsync(GetUserId(), cancellationToken).ConfigureAwait(false);
        if (userProfile == null)
        {
            return NotFound();
        }
        return Ok(userProfile);
    }

    /// <summary>
    /// Updates the profile information for the currently authenticated user.
    /// </summary>
    [HttpPut]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateCurrentUserProfile([FromBody] UpdateUserProfileDto dto, CancellationToken cancellationToken)
    {
        var (success, errorMessage) = await _authService.UpdateCurrentUserProfileAsync(GetUserId(), dto, cancellationToken).ConfigureAwait(false);
        if (!success)
        {
            return BadRequest(new { Message = errorMessage });
        }
        return Ok();
    }

    /// <summary>
    /// Sets or updates the authenticated user's profile picture.
    /// </summary>
    /// <param name="file">The new profile picture file from form data.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    [HttpPut("profile-picture")]
    [RequestSizeLimitFilterFactory("Minio:MaxFileSize")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    public async Task<IActionResult> SetProfilePicture(IFormFile file, CancellationToken cancellationToken)
    {
        await _profilePictureService.SetAsync(GetUserId(), file, true, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    /// <summary>
    /// Removes the authenticated user's profile picture.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    [HttpDelete("profile-picture")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveProfilePicture(CancellationToken cancellationToken)
    {
        var (success, errorMessage) = await _profilePictureService.RemoveAsync(GetUserId(), true, cancellationToken).ConfigureAwait(false);
        return success ? NoContent() : NotFound(errorMessage);
    }
}