using MeetingSystem.Business.Configuration;
using MeetingSystem.Business.Dtos;
using MeetingSystem.Context;
using MeetingSystem.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MeetingSystem.Business;

/// <summary>
/// Defines the contract for a service that manages files associated with meetings.
/// </summary>
public interface IMeetingFileService
{
    /// <summary>
    /// Handles the business logic for uploading multiple files to a meeting.
    /// </summary>
    /// <param name="meetingId">The ID of the meeting to associate the files with.</param>
    /// <param name="files">A collection of files being uploaded from the HTTP request.</param>
    /// <param name="userId">The ID of the user performing the upload (must be a participant).</param>
    /// <param name="commit">A flag indicating whether to commit the transaction.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A tuple containing a list of file metadata DTOs on success, or an error message on failure.</returns>
    Task<(List<FileDto>? Files, string? ErrorMessage)> UploadAsync(Guid meetingId, IFormFileCollection files, Guid userId, bool commit = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a file from a meeting, enforcing authorization rules.
    /// </summary>
    /// <param name="meetingId">The ID of the meeting.</param>
    /// <param name="fileId">The ID of the file to remove.</param>
    /// <param name="userId">The ID of the user attempting to remove the file.</param>
    /// <param name="commit">A flag indicating whether to commit the transaction.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A tuple containing the true status and a null error message on success.
    /// On failure, returns false status and a descriptive error message.
    /// </returns>
    Task<(bool Status, string? ErrorMessage)> RemoveAsync(Guid meetingId, Guid fileId, Guid userId, bool commit = true, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implements the business logic for managing files within a meeting.
/// </summary>
public class MeetingFileService : IMeetingFileService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGenericFileService _genericFileService;
    private readonly ILogger<MeetingFileService> _logger;
    private readonly string _bucketName;

    /// <summary>
    /// Initializes a new instance of the <see cref="MeetingFileService"/> class.
    /// </summary>
    public MeetingFileService(
        IUnitOfWork unitOfWork,
        IGenericFileService genericFileService,
        IOptions<MinioSettings> minioSettings,
        ILogger<MeetingFileService> logger)
    {
        _unitOfWork = unitOfWork;
        _genericFileService = genericFileService;
        _bucketName = minioSettings.Value.Buckets.Meeting;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<(List<FileDto>? Files, string? ErrorMessage)> UploadAsync(Guid meetingId, IFormFileCollection files, Guid userId, bool commit = true, CancellationToken cancellationToken = default)
    {
        var meetingExists = await _unitOfWork.Meetings
            .Find(m => m.Id == meetingId)
            .AnyAsync(cancellationToken)
            .ConfigureAwait(false);

        if (!meetingExists)
        {
            _logger.LogWarning("Upload failed: Meeting {MeetingId} not found.", meetingId);
            return (null, "Meeting not found.");
        }

        var isParticipant = await _unitOfWork.MeetingParticipants
            .Find(p => p.MeetingId == meetingId && p.UserId == userId)
            .AnyAsync(cancellationToken)
            .ConfigureAwait(false);
            
        if (!isParticipant)
        {
            _logger.LogWarning("Upload failed: User {UserId} is not a participant of meeting {MeetingId}.", userId, meetingId);
            return (null, "User is not a participant of this meeting.");
        }

        if (commit)
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        }
        try
        {
            var uploadedFiles = new List<FileDto>();
            foreach (var file in files)
            {
                var meetingFile = new MeetingFile
                {
                    Id = Guid.NewGuid(),
                    MeetingId = meetingId,
                    FileName = file.FileName,
                    ContentType = file.ContentType,
                    SizeBytes = file.Length,
                    UploadedByUserId = userId,
                    MinioObjectKey = $"{meetingId}/{Guid.NewGuid()}-{file.FileName}"
                };

                await _genericFileService.UploadObjectAsync(_bucketName, meetingFile.MinioObjectKey, file, true, cancellationToken).ConfigureAwait(false);

                _unitOfWork.MeetingFiles.Add(meetingFile);
                uploadedFiles.Add(new FileDto(meetingFile.Id, meetingFile.FileName, meetingFile.ContentType, meetingFile.SizeBytes));
            }

            if (commit)
            {
                await _unitOfWork.CommitTransactionAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await _unitOfWork.CompleteAsync(cancellationToken).ConfigureAwait(false);
            }
            return (uploadedFiles, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to complete file upload transaction for MeetingId {MeetingId}. Rolling back.", meetingId);
            if (commit)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken).ConfigureAwait(false);
            }
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<(bool Status, string? ErrorMessage)> RemoveAsync(Guid meetingId, Guid fileId, Guid userId, bool commit = true, CancellationToken cancellationToken = default)
    {
        if (commit)
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        }
        try
        {
            var file = await _unitOfWork.MeetingFiles
                .Find(f => f.Id == fileId && f.MeetingId == meetingId)
                .Include(f => f.Meeting)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            if (file == null)
            {
                _logger.LogWarning("Remove file failed: File {FileId} not found in meeting {MeetingId}.", fileId, meetingId);
                return (false, "File not found.");
            }

            // Business Rule: Only the meeting organizer or the original uploader can delete a file.
            bool isOrganizer = file.Meeting!.OrganizerId == userId;
            bool isUploader = file.UploadedByUserId == userId;

            if (!isOrganizer && !isUploader)
            {
                _logger.LogWarning("Remove file failed: User {UserId} is not authorized to delete file {FileId}.", userId, fileId);
                return (false, "User is not authorized to delete this file.");
            }

            await _genericFileService.RemoveObjectAsync(_bucketName, file.MinioObjectKey, cancellationToken).ConfigureAwait(false);
            
            _unitOfWork.MeetingFiles.Remove(file);

            if (commit)
            {
                await _unitOfWork.CommitTransactionAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await _unitOfWork.CompleteAsync(cancellationToken).ConfigureAwait(false);
            }

            return (true, null); // Success
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to complete file removal transaction for FileId {FileId}. Rolling back.", fileId);
            if (commit)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken).ConfigureAwait(false);
            }
            throw;
        }
    }
}