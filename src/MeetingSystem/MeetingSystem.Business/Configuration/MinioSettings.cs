namespace MeetingSystem.Business.Configuration;

/// <summary>
/// Represents the configuration settings for the MinIO object storage service.
/// This class is bound to the "Minio" section of the application's configuration.
/// </summary>
public class MinioSettings
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "Minio";

    /// <summary>
    /// Gets the public-facing endpoint for MinIO, used for generating client-accessible URLs.
    /// e.g., "http://localhost:9000"
    /// </summary>
    public required string PublicEndpoint { get; init; }

    /// <summary>
    /// Gets the file size in bytes above which files should be compressed before uploading.
    /// </summary>
    public int CompressionFileSizeLimit { get; init; }

    /// <summary>
    /// Gets the names of the buckets used for different file types.
    /// </summary>
    public required Buckets Buckets { get; init; }
}

/// <summary>
/// Represents the specific bucket names used within MinIO.
/// </summary>
public class Buckets
{
    /// <summary>
    /// Gets the name of the bucket used for storing user profile pictures.
    /// </summary>
    public required string Profile { get; init; }

    /// <summary>
    /// Gets the name of the bucket used for storing files attached to meetings.
    /// </summary>
    public required string Meeting { get; init; }
}