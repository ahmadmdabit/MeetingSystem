namespace MeetingSystem.Business.Dtos;

/// <summary>
/// DTO for assigning a role to a user.
/// </summary>
/// <param name="RoleName">The name of the role to assign.</param>
public record AssignRoleDto(string RoleName);
