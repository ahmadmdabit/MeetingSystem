namespace MeetingSystem.Business.Dtos;

/// <summary>
/// Represents the data required to create a new meeting.
/// </summary>
/// <param name="Name">The name or title of the meeting.</param>
/// <param name="Description">A detailed description of the meeting's purpose.</param>
/// <param name="StartAt">The scheduled start time of the meeting in UTC.</param>
/// <param name="EndAt">The scheduled end time of the meeting in UTC.</param>
/// <param name="ParticipantEmails">An optional collection of emails for users to invite to the meeting.</param>
public record CreateMeetingDto(
    string Name,
    string Description,
    DateTime StartAt,
    DateTime EndAt,
    ICollection<string>? ParticipantEmails
);

/// <summary>
/// Represents the data required to update an existing meeting's core details and participant list.
/// </summary>
/// <param name="Name">The updated name or title of the meeting.</param>
/// <param name="Description">The updated description of the meeting.</param>
/// <param name="StartAt">The updated start time of the meeting in UTC.</param>
/// <param name="EndAt">The updated end time of the meeting in UTC.</param>
/// <param name="ParticipantEmails">The definitive collection of emails for users who should be in the meeting (excluding the organizer).</param>
public record UpdateMeetingDto(
    string Name,
    string Description,
    DateTime StartAt,
    DateTime EndAt,
    ICollection<string>? ParticipantEmails
);

/// <summary>
/// Represents the detailed view of a meeting, including its participants.
/// </summary>
/// <param name="Id">The unique identifier for the meeting.</param>
/// <param name="Name">The name of the meeting.</param>
/// <param name="Description">The description of the meeting.</param>
/// <param name="StartAt">The start time of the meeting in UTC.</param>
/// <param name="EndAt">The end time of the meeting in UTC.</param>
/// <param name="OrganizerId">The unique identifier of the user who organized the meeting.</param>
/// <param name="IsCanceled">A flag indicating if the meeting has been canceled.</param>
/// <param name="Participants">A collection of participants attending the meeting.</param>
public record MeetingDto(
    Guid Id,
    string Name,
    string Description,
    DateTime StartAt,
    DateTime EndAt,
    Guid OrganizerId,
    bool IsCanceled,
    ICollection<ParticipantDto> Participants
);

/// <summary>
/// Represents the data required to add a single participant to a meeting.
/// </summary>
/// <param name="Email">The email address of the user to add.</param>
public record AddParticipantDto(string Email);

/// <summary>
/// Represents a participant in a meeting, containing their public user information.
/// </summary>
/// <param name="UserId">The unique identifier of the participant.</param>
/// <param name="FirstName">The first name of the participant.</param>
/// <param name="LastName">The last name of the participant.</param>
/// <param name="Email">The email address of the participant.</param>
public record ParticipantDto(
    Guid UserId,
    string FirstName,
    string LastName,
    string Email
);