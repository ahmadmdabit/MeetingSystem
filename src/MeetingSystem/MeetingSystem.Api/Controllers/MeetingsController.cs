using System.Security.Claims;

using MeetingSystem.Business;
using MeetingSystem.Business.Dtos;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MeetingSystem.Api.Controllers;

/// <summary>
/// Manages meeting-related operations, including creation, retrieval, updates, and participant management.
/// </summary>
[ApiController]
[Route("api/meetings")] // Changed route to be more RESTful
[Authorize]
[Produces("application/json")]
public class MeetingsController : ControllerBase
{
    private readonly IMeetingService _meetingService;

    /// <summary>
    /// Initializes a new instance of the <see cref="MeetingsController"/> class.
    /// </summary>
    /// <param name="meetingService">The service responsible for meeting business logic.</param>
    public MeetingsController(IMeetingService meetingService)
    {
        _meetingService = meetingService;
    }

    /// <summary>
    /// Gets the authenticated user's ID from the JWT claims.
    /// </summary>
    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    /// Creates a new meeting.
    /// </summary>
    /// <remarks>
    /// The authenticated user who creates the meeting is automatically set as the organizer.
    /// A list of participant emails can be provided to invite them from the start.
    /// </remarks>
    /// <param name="dto">The data for the new meeting, including optional participant emails.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The newly created meeting details, including the full participant list.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(MeetingDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateMeeting([FromBody] CreateMeetingDto dto, CancellationToken cancellationToken)
    {
        var meeting = await _meetingService.CreateMeetingAsync(dto, GetUserId(), true, cancellationToken).ConfigureAwait(false);
        return CreatedAtAction(nameof(GetMeetingById), new { id = meeting!.Id }, meeting);
    }

    /// <summary>
    /// Gets a list of all meetings the authenticated user is a participant in.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A list of meetings with their full participant details.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<MeetingDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMeetings(CancellationToken cancellationToken)
    {
        var meetings = await _meetingService.GetUserMeetingsAsync(GetUserId(), cancellationToken).ConfigureAwait(false);
        return Ok(meetings);
    }

    /// <summary>
    /// Gets the details of a specific meeting by its ID.
    /// </summary>
    /// <remarks>
    /// The user must be a participant in the meeting to retrieve its details.
    /// </remarks>
    /// <param name="id">The unique identifier of the meeting.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The meeting details, including the full participant list.</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(MeetingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMeetingById(Guid id, CancellationToken cancellationToken)
    {
        var meeting = await _meetingService.GetMeetingByIdAsync(id, GetUserId(), cancellationToken).ConfigureAwait(false);
        return meeting == null ? NotFound() : Ok(meeting);
    }

    /// <summary>
    /// Updates an existing meeting's core details.
    /// </summary>
    /// <remarks>
    /// Only the organizer of the meeting is allowed to perform this action.
    /// </remarks>
    /// <param name="id">The ID of the meeting to update.</param>
    /// <param name="dto">The updated meeting data.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The full, updated meeting resource.</returns>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(MeetingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateMeeting(Guid id, [FromBody] UpdateMeetingDto dto, CancellationToken cancellationToken)
    {
        var (updatedMeeting, errorMessage) = await _meetingService.UpdateMeetingAsync(id, dto, GetUserId(), cancellationToken).ConfigureAwait(false);

        if (updatedMeeting == null)
        {
            if (errorMessage?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true)
            {
                return NotFound(errorMessage);
            }
            return Forbid(errorMessage!);
        }

        return Ok(updatedMeeting);
    }

    /// <summary>
    /// Cancels a meeting (performs a soft delete).
    /// </summary>
    /// <remarks>
    /// Only the organizer of the meeting is allowed to perform this action.
    /// </remarks>
    /// <param name="id">The ID of the meeting to cancel.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CancelMeeting(Guid id, CancellationToken cancellationToken)
    {
        var (success, errorMessage) = await _meetingService.CancelMeetingAsync(id, GetUserId(), cancellationToken).ConfigureAwait(false);

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

    /// <summary>
    /// Adds a participant to a meeting.
    /// </summary>
    /// <remarks>
    /// Only the organizer of the meeting is allowed to perform this action.
    /// </remarks>
    /// <param name="id">The ID of the meeting.</param>
    /// <param name="dto">The email of the user to add as a participant.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    [HttpPost("{id:guid}/participants")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AddParticipant(Guid id, [FromBody] AddParticipantDto dto, CancellationToken cancellationToken)
    {
        var (success, errorMessage) = await _meetingService.AddParticipantAsync(id, dto.Email, GetUserId(), cancellationToken).ConfigureAwait(false);
        return success ? Ok() : BadRequest(errorMessage);
    }

    /// <summary>
    /// Removes a participant from a meeting.
    /// </summary>
    /// <remarks>
    /// Only the organizer of the meeting is allowed to perform this action. The organizer cannot remove themselves.
    /// </remarks>
    /// <param name="id">The ID of the meeting.</param>
    /// <param name="userId">The ID of the user to remove.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    [HttpDelete("{id:guid}/participants/{userId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RemoveParticipant(Guid id, Guid userId, CancellationToken cancellationToken)
    {
        var (success, errorMessage) = await _meetingService.RemoveParticipantAsync(id, userId, GetUserId(), cancellationToken).ConfigureAwait(false);
        return success ? NoContent() : BadRequest(errorMessage);
    }
}