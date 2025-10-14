using Hangfire;
using MeetingSystem.Business.Dtos;
using MeetingSystem.Business.Jobs;
using MeetingSystem.Context;
using MeetingSystem.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MeetingSystem.Business;

/// <summary>
/// Defines the contract for the service that manages core meeting business logic.
/// </summary>
public interface IMeetingService
{
    /// <summary>
    /// Creates a new meeting and sets the creator as the organizer.
    /// </summary>
    /// <param name="dto">The data for the new meeting.</param>
    /// <param name="organizerId">The ID of the user creating the meeting.</param>
    /// <param name="commit">A flag indicating whether to commit the transaction.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A DTO representing the newly created meeting, or null if the organizer was not found.</returns>
    Task<MeetingDto?> CreateMeetingAsync(CreateMeetingDto dto, Guid organizerId, bool commit = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the details of a specific meeting, provided the user is a participant.
    /// </summary>
    /// <param name="meetingId">The ID of the meeting to retrieve.</param>
    /// <param name="userId">The ID of the user requesting the details.</param>
    /// <param name="commit">A flag indicating whether to commit the transaction.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A DTO with the meeting's details, or null if not found or user is not a participant.</returns>
    Task<MeetingDto?> GetMeetingByIdAsync(Guid meetingId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a list of all non-canceled meetings for a specific user.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>An enumerable collection of meeting DTOs.</returns>
    Task<IEnumerable<MeetingDto>> GetUserMeetingsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the details of an existing meeting. Only the organizer can perform this action.
    /// </summary>
    /// <param name="meetingId">The ID of the meeting to update.</param>
    /// <param name="dto">The updated meeting data.</param>
    /// <param name="userId">The ID of the user attempting the update.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A tuple containing the updated <see cref="MeetingDto"/> and a null error message on success.
    /// On failure, returns a null DTO and a descriptive error message.
    /// </returns>
    Task<(MeetingDto? Meeting, string? ErrorMessage)> UpdateMeetingAsync(Guid meetingId, UpdateMeetingDto dto, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a meeting (soft delete). Only the organizer can perform this action.
    /// </summary>
    /// <param name="meetingId">The ID of the meeting to cancel.</param>
    /// <param name="userId">The ID of the user attempting the cancellation.</param>
    /// <returns>
    /// A tuple containing the true status and a null error message on success.
    /// On failure, returns false status and a descriptive error message.
    /// </returns>
    Task<(bool Status, string? ErrorMessage)> CancelMeetingAsync(Guid meetingId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a participant to a meeting. Only the organizer can perform this action.
    /// </summary>
    /// <param name="meetingId">The ID of the meeting.</param>
    /// <param name="participantEmail">The email of the user to add.</param>
    /// <param name="organizerId">The ID of the user performing the action (must be the organizer).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A tuple containing the true status and a null error message on success.
    /// On failure, returns false status and a descriptive error message.
    /// </returns>
    Task<(bool Status, string? ErrorMessage)> AddParticipantAsync(Guid meetingId, string participantEmail, Guid organizerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a participant from a meeting. Only the organizer can perform this action.
    /// </summary>
    /// <param name="meetingId">The ID of the meeting.</param>
    /// <param name="participantId">The ID of the participant to remove.</param>
    /// <param name="organizerId">The ID of the user performing the action (must be the organizer).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A tuple containing the true status and a null error message on success.
    /// On failure, returns false status and a descriptive error message.
    /// </returns>
    Task<(bool Status, string? ErrorMessage)> RemoveParticipantAsync(Guid meetingId, Guid participantId, Guid organizerId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implements the <see cref="IMeetingService"/> to manage all business logic related to meetings.
/// </summary>
public class MeetingService : IMeetingService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly ILogger<MeetingService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MeetingService"/> class.
    /// </summary>
    /// <param name="unitOfWork">The Unit of Work for database operations.</param>
    /// <param name="backgroundJobClient">The client for enqueuing background jobs.</param>
    /// <param name="logger">The logger for recording service events.</param>
    public MeetingService(
        IUnitOfWork unitOfWork,
        IBackgroundJobClient backgroundJobClient,
        ILogger<MeetingService> logger)
    {
        _unitOfWork = unitOfWork;
        _backgroundJobClient = backgroundJobClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<MeetingDto?> CreateMeetingAsync(CreateMeetingDto dto, Guid organizerId, bool commit = true, CancellationToken cancellationToken = default)
    {
        var organizer = await _unitOfWork.Users
            .Find(u => u.Id == organizerId)
            .AsNoTracking()
            .Select(u => new { u.Id, u.Email })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (organizer == null)
        {
            // Throwing here is acceptable as an invalid organizer ID is an exceptional state.
            throw new InvalidOperationException($"Organizer with ID {organizerId} not found.");
        }

        if (commit)
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        }

        Guid newMeetingId;
        try
        {
            var meeting = new Meeting
            {
                Id = Guid.NewGuid(),
                Name = dto.Name,
                Description = dto.Description,
                StartAt = dto.StartAt.ToUniversalTime(),
                EndAt = dto.EndAt.ToUniversalTime(),
                OrganizerId = organizerId
            };
            newMeetingId = meeting.Id;
            _unitOfWork.Meetings.Add(meeting);

            var emailsToInvite = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { organizer.Email };
            if (dto.ParticipantEmails != null)
            {
                foreach (var email in dto.ParticipantEmails)
                {
                    emailsToInvite.Add(email);
                }
            }

            var usersToInvite = await _unitOfWork.Users
                .Find(u => emailsToInvite.Contains(u.Email))
                .Select(u => u.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            foreach (var userId in usersToInvite)
            {
                _unitOfWork.MeetingParticipants.Add(new MeetingParticipant 
                {
                    MeetingId = newMeetingId,
                    UserId = userId,
                    Role = (userId == organizer.Id) ? "Organizer" : "Participant",
                });
            }

            if (commit)
            {
                await _unitOfWork.CommitTransactionAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await _unitOfWork.CompleteAsync(cancellationToken).ConfigureAwait(false);
            }

            var reminderTime = meeting.StartAt.AddMinutes(-20);
            if (reminderTime > DateTime.UtcNow)
            {
                _backgroundJobClient.Schedule<IMeetingJobs>(
                    job => job.SendReminderAsync(newMeetingId, CancellationToken.None), 
                    reminderTime);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create meeting for organizer {OrganizerId}", organizerId);
            if (commit)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken).ConfigureAwait(false);
            }
            throw;
        }

        // Adhere to DRY by reusing the canonical Get method to build the response DTO.
        return await GetMeetingByIdAsync(newMeetingId, organizerId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<MeetingDto?> GetMeetingByIdAsync(Guid meetingId, Guid userId, CancellationToken cancellationToken = default)
    {
        return _unitOfWork.Meetings
            .Find(m => m.Id == meetingId && m.Participants.Any(p => p.UserId == userId))
            .Include(m => m.Participants)
                .ThenInclude(p => p.User)
            .Select(m => new MeetingDto(
                m.Id,
                m.Name,
                m.Description,
                m.StartAt,
                m.EndAt,
                m.OrganizerId,
                m.IsCanceled,
                m.Participants
                    .OrderByDescending(p => p.UserId == m.OrganizerId)
                    .ThenBy(p => p.AddedAt)
                    .Select(p => new ParticipantDto(
                        p.UserId,
                        p.User!.FirstName,
                        p.User!.LastName,
                        p.User!.Email
                    )).ToList()
            ))
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<MeetingDto>> GetUserMeetingsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _unitOfWork.Meetings
            .Find(m => !m.IsCanceled && m.Participants.Any(p => p.UserId == userId))
            .Include(m => m.Participants)
                .ThenInclude(p => p.User)
            .Select(m => new MeetingDto(
                m.Id,
                m.Name,
                m.Description,
                m.StartAt,
                m.EndAt,
                m.OrganizerId,
                m.IsCanceled,
                m.Participants
                    .OrderByDescending(p => p.UserId == m.OrganizerId) 
                    .ThenBy(p => p.AddedAt) 
                    .Select(p => new ParticipantDto(
                        p.UserId,
                        p.User!.FirstName,
                        p.User!.LastName,
                        p.User!.Email
                    )).ToList()
            ))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<(MeetingDto? Meeting, string? ErrorMessage)> UpdateMeetingAsync(Guid meetingId, UpdateMeetingDto dto, Guid userId, CancellationToken cancellationToken = default)
    {
        var meeting = await _unitOfWork.Meetings
            .Find(m => m.Id == meetingId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (meeting == null)
        {
            _logger.LogWarning("Update failed: Meeting {MeetingId} not found.", meetingId);
            return (null, "Meeting not found");
        }

        if (meeting.OrganizerId != userId)
        {
            _logger.LogWarning("Update failed: User {UserId} is not the organizer of meeting {MeetingId}.", userId, meetingId);
            return (null, "User is not authorized to update this meeting.");
        }

        meeting.Name = dto.Name;
        meeting.Description = dto.Description;
        meeting.StartAt = dto.StartAt.ToUniversalTime();
        meeting.EndAt = dto.EndAt.ToUniversalTime();

        await _unitOfWork.CompleteAsync(cancellationToken).ConfigureAwait(false);

        return (await GetMeetingByIdAsync(meetingId, userId, cancellationToken).ConfigureAwait(false), null);
    }

    /// <inheritdoc />
    public async Task<(bool Status, string? ErrorMessage)> CancelMeetingAsync(Guid meetingId, Guid userId, CancellationToken cancellationToken = default)
    {
        var meeting = await _unitOfWork.Meetings
            .Find(m => m.Id == meetingId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (meeting == null)
        {
            _logger.LogWarning("Cancel failed: Meeting {MeetingId} not found.", meetingId);
            return (false, "Meeting not found.");
        }
        
        if (meeting.OrganizerId != userId)
        {
            _logger.LogWarning("Cancel failed: User {UserId} is not the organizer of meeting {MeetingId}.", userId, meetingId);
            return (false, "User is not authorized to cancel this meeting.");
        }

        meeting.IsCanceled = true;
        meeting.CanceledAt = DateTime.UtcNow;

        await _unitOfWork.CompleteAsync(cancellationToken).ConfigureAwait(false);
        return (true, null); // Success
    }

    /// <inheritdoc />
    public async Task<(bool Status, string? ErrorMessage)> AddParticipantAsync(Guid meetingId, string participantEmail, Guid organizerId, CancellationToken cancellationToken = default)
    {
        var meeting = await _unitOfWork.Meetings
            .Find(m => m.Id == meetingId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
            
        if (meeting == null)
        {
            _logger.LogWarning("Add participant failed: Meeting {MeetingId} not found.", meetingId);
            return (false, "Meeting not found.");
        }

        if (meeting.OrganizerId != organizerId)
        {
            _logger.LogWarning("Add participant failed: User {organizerId} is not authorized to add participants to this meeting {MeetingId}.", organizerId, meetingId);
            return (false, "User is not authorized to add participants to this meeting.");
        }

        var userToAdd = await _unitOfWork.Users
            .Find(u => u.Email == participantEmail)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
            
        if (userToAdd == null)
        {
            _logger.LogWarning("Add participant failed: Participant user {participantEmail} not found.", participantEmail);
            return (false, "Participant user not found.");
        }

        var alreadyExists = await _unitOfWork.MeetingParticipants
            .Find(p => p.MeetingId == meetingId && p.UserId == userToAdd.Id)
            .AnyAsync(cancellationToken)
            .ConfigureAwait(false);
            
        if (alreadyExists)
        {
            return (true, null); // Operation is idempotent, success.
        }

        var participant = new MeetingParticipant { MeetingId = meetingId, UserId = userToAdd.Id , Role = "Participant" };
        _unitOfWork.MeetingParticipants.Add(participant);
        await _unitOfWork.CompleteAsync(cancellationToken).ConfigureAwait(false);
        
        return (true, null); // Success
    }

    /// <inheritdoc />
    public async Task<(bool Status, string? ErrorMessage)> RemoveParticipantAsync(Guid meetingId, Guid participantId, Guid organizerId, CancellationToken cancellationToken = default)
    {
        var meeting = await _unitOfWork.Meetings
            .Find(m => m.Id == meetingId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
            
        if (meeting == null)
        {
            _logger.LogWarning("Remove participant failed: Meeting {MeetingId} not found.", meetingId);
            return (false, "Meeting not found.");
        }

        if (meeting.OrganizerId != organizerId)
        {
            _logger.LogWarning("Remove participant failed: User {organizerId} is not authorized to remove participants from this meeting {MeetingId}.", organizerId, meetingId);
            return (false, "User is not authorized to remove participants from this meeting.");
        }

        if (participantId == organizerId)
        {
            _logger.LogWarning("Remove participant failed: The organizer {organizerId} cannot be removed from their own meeting {MeetingId}.", organizerId, meetingId);
            return (false, "The organizer cannot be removed from their own meeting.");
        }

        var participant = await _unitOfWork.MeetingParticipants
            .Find(p => p.MeetingId == meetingId && p.UserId == participantId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
            
        if (participant == null)
        {
            _logger.LogWarning("Remove participant failed: Participant user {participantEmail} not found in this meeting {MeetingId}.", participantId, meetingId);
            return (false, "Participant not found in this meeting.");
        }

        _unitOfWork.MeetingParticipants.Remove(participant);
        await _unitOfWork.CompleteAsync(cancellationToken).ConfigureAwait(false);

        return (true, null); // Success
    }
}