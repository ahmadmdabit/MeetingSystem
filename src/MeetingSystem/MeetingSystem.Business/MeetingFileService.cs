using MeetingSystem.Business.Configuration;
using MeetingSystem.Business.Dtos;
using MeetingSystem.Context;
using MeetingSystem.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
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
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A tuple containing a list of file metadata DTOs on success, or an error message on failure.</returns>
    Task<(List<FileDto>? Files, string? ErrorMessage)> UploadAsync(Guid meetingId, IFormFileCollection files, Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Removes a file from a meeting, enforcing authorization rules.
    /// </summary>
    /// <param name="meetingId">The ID of the meeting.</param>
    /// <param name="fileId">The ID of the file to remove.</param>
    /// <param name="userId">The ID of the user attempting to remove the file.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A tuple indicating success and a corresponding error message if the operation failed.</returns>
    Task<(bool Success, string? ErrorMessage)> RemoveAsync(Guid meetingId, Guid fileId, Guid userId, CancellationToken cancellationToken);
}

/// <summary>
/// Implements the business logic for managing files within a meeting.
/// </summary>
public class MeetingFileService : IMeetingFileService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGenericFileService _genericFileService;
    private readonly string _bucketName;

    /// <summary>
    /// Initializes a new instance of the <see cref="MeetingFileService"/> class.
    /// </summary>
    public MeetingFileService(IUnitOfWork unitOfWork, IGenericFileService genericFileService, IOptions<MinioSettings> minioSettings)
    {
        _unitOfWork = unitOfWork;
        _genericFileService = genericFileService;
        _bucketName = minioSettings.Value.Buckets.Meeting;
    }

    /// <inheritdoc />
    public async Task<(List<FileDto>? Files, string? ErrorMessage)> UploadAsync(Guid meetingId, IFormFileCollection files, Guid userId, CancellationToken cancellationToken)
    {
        var isParticipant = await _unitOfWork.MeetingParticipants
            .Find(p => p.MeetingId == meetingId && p.UserId == userId)
            .AnyAsync(cancellationToken)
            .ConfigureAwait(false);
            
        if (!isParticipant)
        {
            return (null, "User is not a participant of this meeting.");
        }

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

        await _unitOfWork.CompleteAsync(cancellationToken).ConfigureAwait(false);
        return (uploadedFiles, null);
    }

    /// <inheritdoc />
    public async Task<(bool Success, string? ErrorMessage)> RemoveAsync(Guid meetingId, Guid fileId, Guid userId, CancellationToken cancellationToken)
    {
        var file = await _unitOfWork.MeetingFiles
            .Find(f => f.Id == fileId && f.MeetingId == meetingId)
            .Include(f => f.Meeting) // Include meeting to check organizer
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (file == null)
        {
            return (false, "File not found.");
        }

        // Business Rule: Only the meeting organizer or the original uploader can delete a file.
        bool isOrganizer = file.Meeting!.OrganizerId == userId;
        bool isUploader = file.UploadedByUserId == userId;

        if (!isOrganizer && !isUploader)
        {
            return (false, "User is not authorized to delete this file.");
        }

        await _genericFileService.RemoveObjectAsync(_bucketName, file.MinioObjectKey, cancellationToken).ConfigureAwait(false);
        
        _unitOfWork.MeetingFiles.Remove(file);
        await _unitOfWork.CompleteAsync(cancellationToken).ConfigureAwait(false);
        
        return (true, null);
    }
}