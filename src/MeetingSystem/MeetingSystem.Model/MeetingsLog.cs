using System.ComponentModel.DataAnnotations;

namespace MeetingSystem.Model;

/// <summary>
/// Represents an audit log entry for a deleted meeting.
/// This table is populated exclusively by a database trigger.
/// </summary>
public class MeetingsLog
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MeetingsLog"/> class.
    /// </summary>
    public MeetingsLog() { }

    /// <summary>
    /// The unique identifier for the log entry.
    /// </summary>
    [Key]
    public Guid LogId { get; set; }

    /// <summary>
    /// The ID of the meeting record from the original Meetings table.
    /// </summary>
    public Guid OriginalId { get; set; }

    /// <summary>
    /// The UTC timestamp when the record was deleted.
    /// </summary>
    public DateTime DeletedAt { get; set; }

    /// <summary>
    /// A JSON snapshot of the entire deleted row.
    /// </summary>
    [Required]
    public required string RowJson { get; set; }
}