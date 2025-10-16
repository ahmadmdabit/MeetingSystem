using MeetingSystem.Model;

namespace MeetingSystem.Business.Dtos;

/// <summary>
/// DTO for returning the profile information of the authenticated user.
/// </summary>
public record UserProfileDto(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string Phone,
    string? ProfilePictureUrl,
    ICollection<RoleDto>? Roles
);

/// <summary>
/// DTO for updating the profile information of the authenticated user.
/// </summary>
public record UpdateUserProfileDto(
    string FirstName,
    string LastName,
    string Phone
);

/// <summary>
/// DTO for returning the role information of the authenticated user.
/// </summary>
public record RoleDto(
    Guid Id,
    string Name
);