using MeetingSystem.Business.Configuration;
using MeetingSystem.Context;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
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
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    Task SetAsync(Guid userId, IFormFile file, CancellationToken cancellationToken);

    /// <summary>
    /// Removes a user's profile picture from storage and the user's record.
    /// </summary>
    /// <param name="userId">The ID of the user whose profile picture is being removed.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>True if the picture was found and removed; otherwise, false.</returns>
    Task<bool> RemoveAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Generates a short-lived, pre-signed URL to securely download a user's profile picture.
    /// </summary>
    /// <param name="userId">The ID of the user whose profile picture is being requested.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A pre-signed URL string, or null if the user or picture does not exist.</returns>
    Task<string?> GetUrlAsync(Guid userId, CancellationToken cancellationToken);
}

/// <summary>
/// Implements the business logic for managing user profile pictures.
/// </summary>
public class ProfilePictureService : IProfilePictureService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGenericFileService _genericFileService;
    private readonly string _bucketName;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProfilePictureService"/> class.
    /// </summary>
    public ProfilePictureService(IUnitOfWork unitOfWork, IGenericFileService genericFileService, IOptions<MinioSettings> minioSettings)
    {
        _unitOfWork = unitOfWork;
        _genericFileService = genericFileService;
        _bucketName = minioSettings.Value.Buckets.Profile;
    }

    /// <inheritdoc />
    public async Task SetAsync(Guid userId, IFormFile file, CancellationToken cancellationToken)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId, cancellationToken).ConfigureAwait(false) 
            ?? throw new KeyNotFoundException($"User with ID {userId} not found.");

        if (!string.IsNullOrEmpty(user.ProfilePictureUrl))
        {
            await _genericFileService.RemoveObjectAsync(_bucketName, user.ProfilePictureUrl, cancellationToken).ConfigureAwait(false);
        }

        var objectKey = $"{userId}-{Guid.NewGuid()}-{file.FileName}";
        await _genericFileService.UploadObjectAsync(_bucketName, objectKey, file, false, cancellationToken).ConfigureAwait(false);

        user.ProfilePictureUrl = objectKey;
        await _unitOfWork.CompleteAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> RemoveAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (user == null || string.IsNullOrEmpty(user.ProfilePictureUrl))
        {
            return false;
        }

        await _genericFileService.RemoveObjectAsync(_bucketName, user.ProfilePictureUrl, cancellationToken).ConfigureAwait(false);

        user.ProfilePictureUrl = null;
        await _unitOfWork.CompleteAsync(cancellationToken).ConfigureAwait(false);
        return true;
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
            return null;
        }

        return await _genericFileService.GetPresignedUrlAsync(_bucketName, user.ProfilePictureUrl, cancellationToken).ConfigureAwait(false);
    }
}