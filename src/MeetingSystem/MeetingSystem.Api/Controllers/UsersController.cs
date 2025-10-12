using System;
using System.Threading;
using System.Threading.Tasks;
using MeetingSystem.Business;
using MeetingSystem.Business.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MeetingSystem.Api.Controllers;

/// <summary>
/// Provides endpoints for accessing public information about users.
/// </summary>
[ApiController]
[Route("api/users")]
[Produces("application/json")]
public class UsersController : ControllerBase
{
    private readonly IProfilePictureService _profilePictureService;

    /// <summary>
    /// Initializes a new instance of the <see cref="UsersController"/> class.
    /// </summary>
    public UsersController(IProfilePictureService profilePictureService)
    {
        _profilePictureService = profilePictureService;
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
}