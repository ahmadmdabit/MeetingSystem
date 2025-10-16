using System.ComponentModel.DataAnnotations;

namespace MeetingSystem.Model;

/// <summary>
/// Represents a role in the application (e.g., "Admin", "User").
/// </summary>
public class Role
{
    /// <summary>
    /// The unique identifier for the role.
    /// </summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// The name of the role. This should be unique.
    /// </summary>
    [Required]
    public required string Name { get; set; }

    // Navigation property for the join table
    public virtual ICollection<UserRole> UserRoles { get; set; } = [];
}