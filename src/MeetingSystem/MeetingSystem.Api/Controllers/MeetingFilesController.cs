using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MeetingSystem.Api.Filters;
using MeetingSystem.Business;
using MeetingSystem.Business.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MeetingSystem.Api.Controllers;

/// <summary>
/// Manages files associated with a specific meeting.
/// </summary>
[ApiController]
[Route("api/meetings/{meetingId:guid}/files")]
[Authorize]
[Produces("application/json")]
public class MeetingFilesController : ControllerBase
{
    private readonly IMeetingFileService _meetingFileService;
    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    /// Initializes a new instance of the <see cref="MeetingFilesController"/> class.
    /// </summary>
    public MeetingFilesController(IMeetingFileService meetingFileService)
    {
        _meetingFileService = meetingFileService;
    }

    /// <summary>
    /// Uploads one or more files to a specific meeting.
    /// </summary>
    /// <remarks>
    /// The authenticated user must be a participant in the meeting to upload files.
    /// </remarks>
    /// <param name="meetingId">The ID of the meeting to upload files to.</param>
    /// <param name="files">The collection of files from the form data.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A list of metadata for the successfully uploaded files.</returns>
    [HttpPost]
    [RequestSizeLimitFilterFactory("Minio:MaxFileSize")]
    [ProducesResponseType(typeof(List<FileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    public async Task<IActionResult> Upload(Guid meetingId, IFormFileCollection files, CancellationToken cancellationToken)
    {
        var (fileDtos, error) = await _meetingFileService.UploadAsync(meetingId, files, GetUserId(), cancellationToken).ConfigureAwait(false);
        if (error != null)
        {
            return Forbid(error);
        }
        return Ok(fileDtos);
    }

    /// <summary>
    /// Removes a specific file from a meeting.
    /// </summary>
    /// <remarks>
    /// Only the meeting organizer or the user who originally uploaded the file can perform this action.
    /// </remarks>
    /// <param name="meetingId">The ID of the meeting.</param>
    /// <param name="fileId">The ID of the file to remove.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    [HttpDelete("{fileId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Remove(Guid meetingId, Guid fileId, CancellationToken cancellationToken)
    {
        var (success, errorMessage) = await _meetingFileService.RemoveAsync(meetingId, fileId, GetUserId(), cancellationToken).ConfigureAwait(false);
        
        if (!success)
        {
            if (errorMessage?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true)
            {
                return NotFound(errorMessage);
            }
            return Forbid(errorMessage!);
        }

        return NoContent();
    }
}