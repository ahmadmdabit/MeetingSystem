using System.ComponentModel.DataAnnotations;

namespace MeetingSystem.Model;

/// <summary>
/// Represents a user account in the system.
/// </summary>
public class User
{
    /// <summary>
    /// Initializes a new instance of the <see cref="User"/> class.
    /// </summary>
    public User() { }

    /// <summary>
    /// The unique identifier for the user.
    /// </summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// The user's first name.
    /// </summary>
    public required string FirstName { get; set; }

    /// <summary>
    /// The user's last name.
    /// </summary>
    public required string LastName { get; set; }

    /// <summary>
    /// The user's unique email address, used for login.
    /// </summary>
    [Required]
    public required string Email { get; set; }

    /// <summary>
    /// The user's phone number.
    /// </summary>
    public required string Phone { get; set; }

    /// <summary>
    /// The secure hash of the user's password.
    /// </summary>
    public required string PasswordHash { get; set; }

    /// <summary>
    /// The object key for the user's profile picture stored in MinIO. Can be null.
    /// </summary>
    public string? ProfilePictureUrl { get; set; }

    /// <summary>
    /// The UTC timestamp when the user account was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    // ...... Navigation Properties ......

    /// <summary>
    /// The collection of meetings organized by this user.
    /// </summary>
    public virtual ICollection<Meeting> OrganizedMeetings { get; set; } = [];

    /// <summary>
    /// The collection of meeting participation records for this user.
    /// </summary>
    public virtual ICollection<MeetingParticipant> Meetings { get; set; } = [];
}