namespace MeetingSystem.Business.Dtos;

/// <summary>
/// A DTO for returning a pre-signed, short-lived URL to the client for accessing a private file.
/// </summary>
/// <param name="Url">The publicly accessible URL for the resource.</param>
public record PresignedUrlDto(string Url);