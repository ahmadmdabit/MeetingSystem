using System.ComponentModel.DataAnnotations;

namespace MeetingSystem.Model;

/// <summary>
/// Represents a scheduled meeting event.
/// </summary>
public class Meeting
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Meeting"/> class.
    /// </summary>
    public Meeting() { }

    /// <summary>
    /// The unique identifier for the meeting.
    /// </summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// The name or title of the meeting.
    /// </summary>
    [Required]
    public required string Name { get; set; }

    /// <summary>
    /// A detailed description of the meeting's purpose or agenda.
    /// </summary>
    public required string Description { get; set; }

    /// <summary>
    /// The scheduled start time of the meeting in UTC.
    /// </summary>
    public DateTime StartAt { get; set; }

    /// <summary>
    /// The scheduled end time of the meeting in UTC.
    /// </summary>
    public DateTime EndAt { get; set; }

    /// <summary>
    /// Foreign key for the User who organized the meeting.
    /// </summary>
    [Required]
    public Guid OrganizerId { get; set; }

    /// <summary>
    /// A flag indicating if the meeting has been canceled.
    /// </summary>
    public bool IsCanceled { get; set; }

    /// <summary>
    /// The UTC timestamp when the meeting was canceled. Null if not canceled.
    /// </summary>
    public DateTime? CanceledAt { get; set; }

    /// <summary>
    /// The UTC timestamp when the meeting record was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    // --- Navigation Properties ---

    /// <summary>
    /// The user who organized this meeting.
    /// </summary>
    public virtual User? Organizer { get; set; }

    /// <summary>
    /// The collection of participants attending this meeting.
    /// </summary>
    public virtual ICollection<MeetingParticipant> Participants { get; set; } = [];

    /// <summary>
    /// The collection of files associated with this meeting.
    /// </summary>
    public virtual ICollection<MeetingFile> Files { get; set; } = [];
}