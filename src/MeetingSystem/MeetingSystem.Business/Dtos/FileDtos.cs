namespace MeetingSystem.Business.Dtos;

/// <summary>
/// Represents the metadata for a file that has been uploaded.
/// </summary>
/// <param name="Id">The unique identifier of the file record in the database.</param>
/// <param name="FileName">The original name of the file.</param>
/// <param name="ContentType">The MIME type of the file (e.g., "application/pdf").</param>
/// <param name="SizeBytes">The original size of the file in bytes.</param>
/// <param name="UploadedByUserId">The User who uploaded the file.</param>
public record FileDto(Guid Id, string FileName, string ContentType, long SizeBytes, Guid UploadedByUserId);