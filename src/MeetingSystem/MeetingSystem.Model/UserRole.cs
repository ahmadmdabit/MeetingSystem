using System;

namespace MeetingSystem.Model;

/// <summary>
/// Represents the join entity for the many-to-many relationship between Users and Roles.
/// </summary>
public class UserRole
{
    /// <summary>
    /// Foreign key for the User. Part of the composite primary key.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Foreign key for the Role. Part of the composite primary key.
    /// </summary>
    public Guid RoleId { get; set; }

    // Navigation properties
    public virtual User? User { get; set; }
    public virtual Role? Role { get; set; }
}