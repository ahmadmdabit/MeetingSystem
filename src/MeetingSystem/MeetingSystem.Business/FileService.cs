using System;
using System.Threading;
using System.Threading.Tasks;
using MeetingSystem.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Minio;
using Minio.DataModel.Args;

namespace MeetingSystem.Business;

/// <summary>
/// Defines the contract for a service that manages file operations, such as retrieving file URLs.
/// </summary>
public interface IFileService
{
    /// <summary>
    /// Generates a short-lived, pre-signed URL to securely download a user's profile picture.
    /// </summary>
    /// <param name="userId">The ID of the user whose profile picture is being requested.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A pre-signed URL string, or null if the user or picture does not exist.</returns>
    Task<string?> GetUserProfilePictureUrlAsync(Guid userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implements the <see cref="IFileService"/> to interact with a MinIO object storage backend.
/// </summary>
public class FileService : IFileService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMinioClient _minioClient;
    private readonly string _bucketName;
    private readonly string _publicEndpoint;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileService"/> class.
    /// </summary>
    public FileService(IUnitOfWork unitOfWork, IMinioClient minioClient, IConfiguration configuration)
    {
        _unitOfWork = unitOfWork;
        _minioClient = minioClient;
        _bucketName = configuration["Minio:BucketName"]!;
        _publicEndpoint = configuration["Minio:PublicEndpoint"]!;
    }

    /// <inheritdoc />
    public async Task<string?> GetUserProfilePictureUrlAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Users
            .Find(u => u.Id == userId)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (user == null || string.IsNullOrEmpty(user.ProfilePictureUrl))
        {
            return null;
        }

        var publicUri = new Uri(_publicEndpoint);
        var publicMinioClient = new MinioClient()
            .WithEndpoint(publicUri.Host, publicUri.Port)
            .WithCredentials(_minioClient.Config.AccessKey, _minioClient.Config.SecretKey)
            .WithSSL(publicUri.Scheme == "https")
            .Build();

        var args = new PresignedGetObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(user.ProfilePictureUrl)
            .WithExpiry(300);

        return await publicMinioClient.PresignedGetObjectAsync(args).ConfigureAwait(false);
    }
}