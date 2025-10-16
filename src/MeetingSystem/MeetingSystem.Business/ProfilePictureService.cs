using MeetingSystem.Business.Configuration;
using MeetingSystem.Context;

using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MeetingSystem.Business;

/// <summary>
/// Defines the contract for a service that manages user profile pictures.
/// </summary>
public interface IProfilePictureService
{
    /// <summary>
    /// Sets or updates a user's profile picture. Deletes the old picture if one exists.
    /// </summary>
    /// <param name="userId">The ID of the user whose profile picture is being set.</param>
    /// <param name="file">The new profile picture file.</param>
    /// <param name="commit">A flag indicating whether to commit the transaction.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A tuple containing the true status and a null error message on success.
    /// On failure, returns false status and a descriptive error message.
    /// </returns>
    Task<(bool Status, string? ErrorMessage)> SetAsync(Guid userId, IFormFile file, bool commit = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a user's profile picture from storage and the user's record.
    /// </summary>
    /// <param name="userId">The ID of the user whose profile picture is being removed.</param>
    /// <param name="commit">A flag indicating whether to commit the transaction.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A tuple containing the true status and a null error message on success.
    /// On failure, returns false status and a descriptive error message.
    /// </returns>
    Task<(bool Status, string? ErrorMessage)> RemoveAsync(Guid userId, bool commit = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a short-lived, pre-signed URL to securely download a user's profile picture.
    /// </summary>
    /// <param name="userId">The ID of the user whose profile picture is being requested.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A pre-signed URL string, or null if the user or picture does not exist.</returns>
    Task<string?> GetUrlAsync(Guid userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implements the business logic for managing user profile pictures.
/// </summary>
public class ProfilePictureService : IProfilePictureService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGenericFileService _genericFileService;
    private readonly ILogger<ProfilePictureService> _logger;
    private readonly string _bucketName;

    public ProfilePictureService(
        IUnitOfWork unitOfWork,
        IGenericFileService genericFileService,
        IOptions<MinioSettings> minioSettings,
        ILogger<ProfilePictureService> logger)
    {
        _unitOfWork = unitOfWork;
        _genericFileService = genericFileService;
        _bucketName = minioSettings.Value.Buckets.Profile;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<(bool Status, string? ErrorMessage)> SetAsync(Guid userId, IFormFile file, bool commit = true, CancellationToken cancellationToken = default)
    {
        if (commit)
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        }
        try
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId, cancellationToken).ConfigureAwait(false);
            if (user == null)
            {
                _logger.LogWarning("Set profile picture failed: User {UserId} not found.", userId);
                return (false, "User not found.");
            }

            // If an old picture exists, remove it from object storage first.
            if (!string.IsNullOrEmpty(user.ProfilePictureUrl))
            {
                await _genericFileService.RemoveObjectAsync(_bucketName, user.ProfilePictureUrl, cancellationToken).ConfigureAwait(false);
            }

            // Upload the new picture to object storage.
            var objectKey = $"{userId}-{Guid.NewGuid()}-{file.FileName}";
            await _genericFileService.UploadObjectAsync(_bucketName, objectKey, file, false, cancellationToken).ConfigureAwait(false);

            // Update the user record in the database.
            user.ProfilePictureUrl = objectKey;

            // Commit the transaction, saving the database change.
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
            _logger.LogError(ex, "Failed to complete profile picture set transaction for UserId {UserId}. Rolling back.", userId);
            if (commit)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken).ConfigureAwait(false);
            }
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<(bool Status, string? ErrorMessage)> RemoveAsync(Guid userId, bool commit = true, CancellationToken cancellationToken = default)
    {
        if (commit)
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        }
        try
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId, cancellationToken).ConfigureAwait(false);
            if (user == null)
            {
                _logger.LogWarning("Remove profile picture failed: User {UserId} not found.", userId);
                return (false, "User not found.");
            }

            if (string.IsNullOrEmpty(user.ProfilePictureUrl))
            {
                _logger.LogWarning("Remove profile picture failed: User {UserId} has no profile picture to remove.", userId);
                return (false, "User does not have a profile picture.");
            }

            // Remove the picture from object storage.
            await _genericFileService.RemoveObjectAsync(_bucketName, user.ProfilePictureUrl, cancellationToken).ConfigureAwait(false);

            // Update the user record in the database.
            user.ProfilePictureUrl = null;

            // Commit the transaction, saving the database change.
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
            _logger.LogError(ex, "Failed to complete profile picture removal transaction for UserId {UserId}. Rolling back.", userId);
            if (commit)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken).ConfigureAwait(false);
            }
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<string?> GetUrlAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _unitOfWork.Users
            .Find(u => u.Id == userId)
            .AsNoTracking()
            .Select(u => new { u.ProfilePictureUrl })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (user == null || string.IsNullOrEmpty(user.ProfilePictureUrl))
        {
            _logger.LogInformation("Get profile picture URL requested for user {UserId}, but no picture was found.", userId);
            return null;
        }

        return await _genericFileService.GetPresignedUrlAsync(_bucketName, user.ProfilePictureUrl, 86400, cancellationToken).ConfigureAwait(false);
    }
}