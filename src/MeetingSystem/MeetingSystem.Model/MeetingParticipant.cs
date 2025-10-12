namespace MeetingSystem.Model;

/// <summary>
/// Represents the link between a User and a Meeting, indicating participation.
/// This is a join entity for a many-to-many relationship.
/// </summary>
public class MeetingParticipant
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MeetingParticipant"/> class.
    /// </summary>
    public MeetingParticipant() { }

    /// <summary>
    /// Foreign key for the Meeting. Part of the composite primary key.
    /// </summary>
    public Guid MeetingId { get; set; }

    /// <summary>
    /// Foreign key for the User. Part of the composite primary key.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Optional role of the participant, e.g., "Presenter", "Scribe".
    /// </summary>
    public string? Role { get; set; }

    /// <summary>
    /// The UTC timestamp when the participant was added to the meeting.
    /// </summary>
    public DateTime AddedAt { get; set; }

    // ...... Navigation Properties ......

    /// <summary>
    /// The meeting associated with this participation record.
    /// </summary>
    public virtual Meeting? Meeting { get; set; }

    /// <summary>
    /// The user associated with this participation record.
    /// </summary>
    public virtual User? User { get; set; }
}