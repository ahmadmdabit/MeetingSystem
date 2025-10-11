using System.ComponentModel.DataAnnotations;

namespace MeetingSystem.Model;

/// <summary>
/// Represents the metadata for a file associated with a meeting.
/// </summary>
public class MeetingFile
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MeetingFile"/> class.
    /// </summary>
    public MeetingFile() { }

    /// <summary>
    /// The unique identifier for the file record.
    /// </summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// Foreign key for the Meeting this file belongs to.
    /// </summary>
    public Guid MeetingId { get; set; }

    /// <summary>
    /// The original name of the uploaded file.
    /// </summary>
    [Required]
    public required string FileName { get; set; }

    /// <summary>
    /// The MIME type of the file (e.g., "application/pdf").
    /// </summary>
    [Required]
    public required string ContentType { get; set; }

    /// <summary>
    /// The size of the file in bytes.
    /// </summary>
    public long SizeBytes { get; set; }

    /// <summary>
    /// The unique key (path) of the object stored in the MinIO bucket.
    /// e.g., "{meetingId}/{guid}-{originalFilename}"
    /// </summary>
    [Required]
    public required string MinioObjectKey { get; set; }

    /// <summary>
    /// Foreign key for the User who uploaded the file.
    /// </summary>
    [Required]
    public Guid UploadedByUserId { get; set; }

    /// <summary>
    /// The UTC timestamp when the file was uploaded.
    /// </summary>
    public DateTime UploadedAt { get; set; }

    // --- Navigation Properties ---

    /// <summary>
    /// The meeting this file is associated with.
    /// </summary>
    public virtual Meeting? Meeting { get; set; }
}