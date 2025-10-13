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
/// File-related operations are delegated to the IFileService.
/// </summary>
public interface IMeetingService
{
    /// <summary>
    /// Creates a new meeting and sets the creator as the organizer.
    /// </summary>
    /// <param name="dto">The data for the new meeting.</param>
    /// <param name="organizerId">The ID of the user creating the meeting.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A DTO representing the newly created meeting.</returns>
    Task<MeetingDto> CreateMeetingAsync(CreateMeetingDto dto, Guid organizerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the details of a specific meeting, provided the user is a participant.
    /// </summary>
    /// <param name="meetingId">The ID of the meeting to retrieve.</param>
    /// <param name="userId">The ID of the user requesting the details.</param>
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
    /// On failure (e.g., not found or not authorized), returns a null DTO and a descriptive error message.
    /// </returns>
    Task<(MeetingDto? Meeting, string? ErrorMessage)> UpdateMeetingAsync(Guid meetingId, UpdateMeetingDto dto, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a meeting (soft delete). Only the organizer can perform this action.
    /// </summary>
    /// <param name="meetingId">The ID of the meeting to cancel.</param>
    /// <param name="userId">The ID of the user attempting the cancellation.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>True if the cancellation was successful; otherwise, false.</returns>
    Task<bool> CancelMeetingAsync(Guid meetingId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a participant to a meeting. Only the organizer can perform this action.
    /// </summary>
    /// <param name="meetingId">The ID of the meeting.</param>
    /// <param name="participantEmail">The email of the user to add.</param>
    /// <param name="organizerId">The ID of the user performing the action (must be the organizer).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>True if the participant was added successfully; otherwise, false.</returns>
    Task<bool> AddParticipantAsync(Guid meetingId, string participantEmail, Guid organizerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a participant from a meeting. Only the organizer can perform this action.
    /// </summary>
    /// <param name="meetingId">The ID of the meeting.</param>
    /// <param name="participantId">The ID of the participant to remove.</param>
    /// <param name="organizerId">The ID of the user performing the action (must be the organizer).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>True if the participant was removed successfully; otherwise, false.</returns>
    Task<bool> RemoveParticipantAsync(Guid meetingId, Guid participantId, Guid organizerId, CancellationToken cancellationToken = default);
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
    public async Task<MeetingDto> CreateMeetingAsync(CreateMeetingDto dto, Guid organizerId, CancellationToken cancellationToken = default)
    {
        var organizer = await _unitOfWork.Users
            .Find(u => u.Id == organizerId)
            .AsNoTracking()
            .Select(u => new { u.Id, u.FirstName, u.LastName, u.Email })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (organizer == null)
        {
            throw new InvalidOperationException($"Organizer with ID {organizerId} not found.");
        }

        await _unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
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
            _unitOfWork.Meetings.Add(meeting);

            // 1. Prepare a distinct list of emails to invite, always including the organizer at first.
            var emailsToInvite = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            emailsToInvite.Add(organizer.Email); // Always include the organizer first
            if (dto.ParticipantEmails != null)
            {
                foreach (var email in dto.ParticipantEmails)
                {
                    emailsToInvite.Add(email);
                }
            }

            // 2. Perform a single database query to find all valid users for the invite list.
            var usersToInvite = await _unitOfWork.Users
                .Find(u => emailsToInvite.Contains(u.Email))
                .Select(u => new { u.Id, u.FirstName, u.LastName, u.Email })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            // 3. Create the participant records and the final DTO list.
            var participantDtos = new List<ParticipantDto>();
            foreach (var user in usersToInvite)
            {
                _unitOfWork.MeetingParticipants.Add(new MeetingParticipant 
                {
                    MeetingId = meeting.Id,
                    UserId = user.Id,
                    Role = (user.Id == organizer.Id) ? "Organizer" : "Participant",
                });
                participantDtos.Add(new ParticipantDto(user.Id, user.FirstName, user.LastName, user.Email));
            }
            
            await _unitOfWork.CommitTransactionAsync(cancellationToken).ConfigureAwait(false);

            var reminderTime = meeting.StartAt.AddMinutes(-20);
            if (reminderTime > DateTime.UtcNow)
            {
                _backgroundJobClient.Schedule<IMeetingJobs>(
                    job => job.SendReminderAsync(meeting.Id, CancellationToken.None), 
                    reminderTime);
            }

            return new MeetingDto(
                meeting.Id, 
                meeting.Name, 
                meeting.Description, 
                meeting.StartAt, 
                meeting.EndAt, 
                meeting.OrganizerId, 
                meeting.IsCanceled,
                participantDtos
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create meeting for organizer {OrganizerId}", organizerId);
            await _unitOfWork.RollbackTransactionAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc />
    public Task<MeetingDto?> GetMeetingByIdAsync(Guid meetingId, Guid userId, CancellationToken cancellationToken = default)
    {
        // This query now eagerly loads the Participants and their related User data.
        return _unitOfWork.Meetings
        .Find(m => m.Id == meetingId && m.Participants.Any(p => p.UserId == userId))
        .Include(m => m.Participants) // <-- 1. Include the join table
            .ThenInclude(p => p.User) // <-- 2. Then include the User from the join table
        .Select(m => new MeetingDto(
            m.Id,
            m.Name,
            m.Description,
            m.StartAt,
            m.EndAt,
            m.OrganizerId,
            m.IsCanceled,
            // 3. Project the included data into the new ParticipantDto
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
                    // 1. Primary Sort: The organizer always comes first.
                    .OrderByDescending(p => p.UserId == m.OrganizerId) 
                    // 2. Secondary Sort: All other participants are sorted by when they were added.
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
        // Find the meeting first to verify ownership.
        var meeting = await _unitOfWork.Meetings
            .Find(m => m.Id == meetingId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (meeting == null)
        {
            _logger.LogWarning("Update failed: Meeting {MeetingId} not found or user {UserId} is not the organizer.", meetingId, userId);
            return (null, "Meeting not found");
        }

        if (meeting.OrganizerId != userId)
        {
            _logger.LogWarning("Update failed: User {UserId} is not the organizer of meeting {MeetingId}.", userId, meetingId);
            return (null, "Not authorized: User is not the organizer");
        }

        // Apply the updates to the tracked entity.
        meeting.Name = dto.Name;
        meeting.Description = dto.Description;
        meeting.StartAt = dto.StartAt.ToUniversalTime();
        meeting.EndAt = dto.EndAt.ToUniversalTime();

        // Save the changes.
        await _unitOfWork.CompleteAsync(cancellationToken).ConfigureAwait(false);

        return (await GetMeetingByIdAsync(meetingId, userId, cancellationToken).ConfigureAwait(false), null);
    }

    /// <inheritdoc />
    public async Task<bool> CancelMeetingAsync(Guid meetingId, Guid userId, CancellationToken cancellationToken = default)
    {
        var meeting = await _unitOfWork.Meetings
            .Find(m => m.Id == meetingId && m.OrganizerId == userId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (meeting == null)
        {
            _logger.LogWarning("Cancel failed: Meeting {MeetingId} not found or user {UserId} is not the organizer.", meetingId, userId);
            return false;
        }

        meeting.IsCanceled = true;
        meeting.CanceledAt = DateTime.UtcNow;

        await _unitOfWork.CompleteAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> AddParticipantAsync(Guid meetingId, string participantEmail, Guid organizerId, CancellationToken cancellationToken = default)
    {
        var meeting = await _unitOfWork.Meetings
            .Find(m => m.Id == meetingId && m.OrganizerId == organizerId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
            
        if (meeting == null) return false;

        var userToAdd = await _unitOfWork.Users
            .Find(u => u.Email == participantEmail)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
            
        if (userToAdd == null) return false;

        var alreadyExists = await _unitOfWork.MeetingParticipants
            .Find(p => p.MeetingId == meetingId && p.UserId == userToAdd.Id)
            .AnyAsync(cancellationToken)
            .ConfigureAwait(false);
            
        if (alreadyExists) return true; // Operation is idempotent

        var participant = new MeetingParticipant { MeetingId = meetingId, UserId = userToAdd.Id , Role = "Participant" };
        _unitOfWork.MeetingParticipants.Add(participant);
        await _unitOfWork.CompleteAsync(cancellationToken).ConfigureAwait(false);
        
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> RemoveParticipantAsync(Guid meetingId, Guid participantId, Guid organizerId, CancellationToken cancellationToken = default)
    {
        var meeting = await _unitOfWork.Meetings
            .Find(m => m.Id == meetingId && m.OrganizerId == organizerId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
            
        if (meeting == null || participantId == organizerId)
        {
            // Fail if not organizer or if trying to remove self.
            return false;
        }

        var participant = await _unitOfWork.MeetingParticipants
            .Find(p => p.MeetingId == meetingId && p.UserId == participantId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
            
        if (participant == null) return false;

        _unitOfWork.MeetingParticipants.Remove(participant);
        await _unitOfWork.CompleteAsync(cancellationToken).ConfigureAwait(false);

        return true;
    }
}