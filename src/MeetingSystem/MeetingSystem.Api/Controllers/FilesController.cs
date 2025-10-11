using System;
using System.Threading;
using System.Threading.Tasks;
using MeetingSystem.Business;
using MeetingSystem.Business.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MeetingSystem.Api.Controllers;

/// <summary>
/// Manages file-related operations, such as retrieving profile pictures.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FilesController : ControllerBase
{
    private readonly IFileService _fileService;

    public FilesController(IFileService fileService)
    {
        _fileService = fileService;
    }

    /// <summary>
    /// Gets a secure, short-lived URL for a user's profile picture.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="cancellationToken">A token to cancel the operation if the client disconnects.</param>
    [HttpGet("users/{userId}/profile-picture")]
    [Authorize(Roles = "Admin,User")]
    [ProducesResponseType(typeof(PresignedUrlDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserProfilePictureUrl(Guid userId, CancellationToken cancellationToken)
    {
        var url = await _fileService.GetUserProfilePictureUrlAsync(userId, cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrEmpty(url))
        {
            return NotFound();
        }

        return Ok(new PresignedUrlDto(url));
    }
}