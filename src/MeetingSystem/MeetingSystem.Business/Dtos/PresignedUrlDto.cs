namespace MeetingSystem.Business.Dtos;

/// <summary>
/// A DTO for returning a pre-signed URL to the client.
/// </summary>
public record PresignedUrlDto(string Url);