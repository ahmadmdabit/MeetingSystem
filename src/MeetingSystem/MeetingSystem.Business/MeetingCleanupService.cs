using MeetingSystem.Business.Configuration;
using MeetingSystem.Context;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MeetingSystem.Business;

/// <summary>
/// Defines the contract for a service that performs scheduled cleanup of old, canceled meetings.
/// </summary>
public interface IMeetingCleanupService
{
    /// <summary>
    /// Finds and permanently deletes meetings that were canceled more than 30 days ago,
    /// including all associated files in object storage.
    /// </summary>
    /// <param name="commit">A value indicating whether the changes should be committed to the database.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    Task CleanUpAsync(bool commit = true, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implements the scheduled cleanup logic for permanently deleting old, canceled meetings.
/// </summary>
public class MeetingCleanupService : IMeetingCleanupService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMeetingFileService _meetingFileService;
    private readonly ILogger<MeetingCleanupService> _logger;
    private readonly HangfireSettings _hangfireSettings;

    public MeetingCleanupService(
        IUnitOfWork unitOfWork,
        IMeetingFileService meetingFileService,
        IOptions<MinioSettings> minioSettings,
        IOptions<HangfireSettings> hangfireSettings,
        ILogger<MeetingCleanupService> logger)
    {
        _unitOfWork = unitOfWork;
        _meetingFileService = meetingFileService;
        _hangfireSettings = hangfireSettings.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task CleanUpAsync(bool commit = true, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting scheduled meeting cleanup job. Threshold: {Days} days.", _hangfireSettings.CleanupThresholdDays);

        var cutoffDate = DateTime.UtcNow.AddDays(-_hangfireSettings.CleanupThresholdDays);

        var meetingsToClean = await _unitOfWork.Meetings
            .Find(m => m.IsCanceled && m.CanceledAt.HasValue && m.CanceledAt.Value < cutoffDate)
            .Include(m => m.Files)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (meetingsToClean.Count == 0)
        {
            _logger.LogInformation("No meetings found for cleanup.");
            return;
        }

        _logger.LogInformation("Found {Count} meetings to permanently delete.", meetingsToClean.Count);

        if (commit)
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        }
        try
        {
            foreach (var meeting in meetingsToClean)
            {
                // Create a list of file IDs to avoid issues with modifying the collection while iterating
                var fileIds = meeting.Files.Select(f => f.Id).ToList();
                foreach (var fileId in fileIds)
                {
                    // Call the composable, non-committing method
                    var (success, errorMessage) = await _meetingFileService.RemoveAsync(meeting.Id, fileId, meeting.OrganizerId, false, cancellationToken).ConfigureAwait(false);
                    if (!success)
                    {
                        throw new InvalidOperationException($"Failed to queue removal for file {fileId} in meeting {meeting.Id}: {errorMessage}");
                    }
                }
            }

            _unitOfWork.Meetings.RemoveRange(meetingsToClean);

            if (commit)
            {
                await _unitOfWork.CommitTransactionAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await _unitOfWork.CompleteAsync(cancellationToken).ConfigureAwait(false);
            }
            _logger.LogInformation("Successfully deleted {Count} meetings and their associated files.", meetingsToClean.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to complete meeting cleanup job. Rolling back transaction.");
            if (commit)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken).ConfigureAwait(false);
            }
            throw;
        }
    }
}