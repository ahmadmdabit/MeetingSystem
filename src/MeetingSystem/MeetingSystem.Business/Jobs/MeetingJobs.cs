using MeetingSystem.Context;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MeetingSystem.Business.Jobs;

/// <summary>
/// Defines the contract for background jobs related to meetings.
/// </summary>
public interface IMeetingJobs
{
    /// <summary>
    /// Fetches a meeting's details and sends a reminder email to all participants.
    /// This method is designed to be called by Hangfire.
    /// </summary>
    /// <param name="meetingId">The ID of the meeting to send a reminder for.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    Task SendReminderAsync(Guid meetingId, CancellationToken cancellationToken);
}

/// <summary>
/// Implements the background job logic for meetings.
/// </summary>
public class MeetingJobs : IMeetingJobs
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmailService _emailService;
    private readonly ILogger<MeetingJobs> _logger;

    public MeetingJobs(IUnitOfWork unitOfWork, IEmailService emailService, ILogger<MeetingJobs> logger)
    {
        _unitOfWork = unitOfWork;
        _emailService = emailService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task SendReminderAsync(Guid meetingId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Executing reminder job for meeting {MeetingId}", meetingId);

        // Use IQueryable to build an efficient query
        var meetingDetails = await _unitOfWork.Meetings
            .Find(m => m.Id == meetingId)
            .Select(m => new
            {
                m.Name,
                m.StartAt,
                Participants = m.Participants.Select(p => p.User!.Email).ToList()
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (meetingDetails == null)
        {
            _logger.LogWarning("Meeting {MeetingId} not found while trying to send reminder. The meeting may have been deleted.", meetingId);
            return;
        }

        _logger.LogInformation("Found {ParticipantCount} participants for meeting {MeetingId}", meetingDetails.Participants.Count, meetingId);

        foreach (var participantEmail in meetingDetails.Participants)
        {
            await _emailService.SendMeetingReminderAsync(
                participantEmail,
                meetingDetails.Name,
                meetingDetails.StartAt,
                cancellationToken)
            .ConfigureAwait(false);
        }

        _logger.LogInformation("Successfully sent reminders for meeting {MeetingId}", meetingId);
    }
}