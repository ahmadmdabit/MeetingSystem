using System.IO.Compression;

using MeetingSystem.Business.Configuration;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;

namespace MeetingSystem.Business;

/// <summary>
/// Defines the contract for a low-level, generic service that interacts with the object storage.
/// This service is not aware of any specific business domain.
/// </summary>
public interface IGenericFileService
{
    /// <summary>
    /// Generates a public-facing, pre-signed URL for an object.
    /// </summary>
    /// <param name="bucketName">The name of the bucket.</param>
    /// <param name="objectKey">The key of the object.</param>
    /// <param name="expiry">The expiration duration in seconds.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A short-lived, publicly accessible URL.</returns>
    Task<string> GetPresignedUrlAsync(string bucketName, string objectKey, int expiry = 300, CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads a file stream to the specified bucket.
    /// </summary>
    /// <param name="bucketName">The name of the target bucket.</param>
    /// <param name="objectKey">The unique key for the new object.</param>
    /// <param name="file">The file to upload.</param>
    /// <param name="allowCompression">A flag to indicate if compression should be applied for large files.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    Task UploadObjectAsync(string bucketName, string objectKey, IFormFile file, bool allowCompression, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes an object from a bucket.
    /// </summary>
    /// <param name="bucketName">The name of the bucket.</param>
    /// <param name="objectKey">The key of the object to remove.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    Task RemoveObjectAsync(string bucketName, string objectKey, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implements the <see cref="IGenericFileService"/> using the MinIO client.
/// This class contains the reusable, low-level logic for object storage operations.
/// </summary>
public class GenericFileService : IGenericFileService
{
    private readonly IMinioClient _minioClient;
    private readonly MinioSettings _minioSettings;
    private readonly ILogger<GenericFileService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenericFileService"/> class.
    /// </summary>
    public GenericFileService(IMinioClient minioClient, IOptions<MinioSettings> minioSettings, ILogger<GenericFileService> logger)
    {
        _minioClient = minioClient;
        _minioSettings = minioSettings.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<string> GetPresignedUrlAsync(string bucketName, string objectKey, int expiry = 300, CancellationToken cancellationToken = default)
    {
        var publicUri = new Uri(_minioSettings.PublicEndpoint);
        // Create a temporary client configured with the public-facing endpoint for URL generation.
        var publicMinioClient = new MinioClient()
            .WithEndpoint(publicUri.Host, publicUri.Port)
            .WithCredentials(_minioClient.Config.AccessKey, _minioClient.Config.SecretKey)
            .WithSSL(publicUri.Scheme == "https")
            .Build();

        var args = new PresignedGetObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectKey)
            .WithExpiry(expiry);

        return publicMinioClient.PresignedGetObjectAsync(args);
    }

    /// <inheritdoc />
    public async Task UploadObjectAsync(string bucketName, string objectKey, IFormFile file, bool allowCompression, CancellationToken cancellationToken = default)
    {
        var bucketExistsArgs = new BucketExistsArgs().WithBucket(bucketName);
        if (!await _minioClient.BucketExistsAsync(bucketExistsArgs, cancellationToken).ConfigureAwait(false))
        {
            await _minioClient.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucketName), cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Created MinIO bucket: {BucketName}", bucketName);
        }

        await using var stream = file.OpenReadStream();
        if (allowCompression && file.Length > _minioSettings.CompressionFileSizeLimit)
        {
            await using var memoryStream = new MemoryStream();
            await using (var gzipStream = new GZipStream(memoryStream, CompressionMode.Compress, true))
            {
                await stream.CopyToAsync(gzipStream, cancellationToken).ConfigureAwait(false);
            }
            memoryStream.Position = 0;

            var putCompressedArgs = new PutObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectKey)
                .WithStreamData(memoryStream)
                .WithObjectSize(memoryStream.Length)
                .WithContentType("application/gzip");
            await _minioClient.PutObjectAsync(putCompressedArgs, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            var putArgs = new PutObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectKey)
                .WithStreamData(stream)
                .WithObjectSize(file.Length)
                .WithContentType(file.ContentType);
            await _minioClient.PutObjectAsync(putArgs, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task RemoveObjectAsync(string bucketName, string objectKey, CancellationToken cancellationToken = default)
    {
        try
        {
            var removeArgs = new RemoveObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectKey);
            await _minioClient.RemoveObjectAsync(removeArgs, cancellationToken).ConfigureAwait(false);
        }
        catch (ObjectNotFoundException)
        {
            _logger.LogWarning("Attempted to delete object '{ObjectKey}' from bucket '{BucketName}', but it was not found.", objectKey, bucketName);
            // Do not re-throw; the desired state (object is gone) is achieved.
        }
    }
}