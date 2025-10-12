using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MeetingSystem.Api.Filters;
using MeetingSystem.Business;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MeetingSystem.Api.Controllers;

/// <summary>
/// Manages the authenticated user's own profile picture.
/// </summary>
[ApiController]
[Route("api/users/me/profile-picture")]
[Authorize]
[Produces("application/json")]
public class UserProfileController : ControllerBase
{
    private readonly IProfilePictureService _profilePictureService;
    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    /// Initializes a new instance of the <see cref="UserProfileController"/> class.
    /// </summary>
    public UserProfileController(IProfilePictureService profilePictureService)
    {
        _profilePictureService = profilePictureService;
    }

    /// <summary>
    /// Sets or updates the authenticated user's profile picture.
    /// </summary>
    /// <param name="file">The new profile picture file from form data.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    [HttpPut]
    [RequestSizeLimitFilterFactory("Minio:MaxFileSize")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    public async Task<IActionResult> Set(IFormFile file, CancellationToken cancellationToken)
    {
        await _profilePictureService.SetAsync(GetUserId(), file, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    /// <summary>
    /// Removes the authenticated user's profile picture.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Remove(CancellationToken cancellationToken)
    {
        var success = await _profilePictureService.RemoveAsync(GetUserId(), cancellationToken).ConfigureAwait(false);
        return success ? NoContent() : NotFound("Profile picture not found or already removed.");
    }
}