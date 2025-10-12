namespace MeetingSystem.Business.Configuration;

/// <summary>
/// Represents the configuration settings for the SMTP email service.
/// This class is bound to the "Smtp" section of the application's configuration.
/// </summary>
public class SmtpSettings
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "Smtp";

    /// <summary>
    /// Gets the hostname of the SMTP server.
    /// e.g., "mailpit" (in Docker) or "smtp.sendgrid.net" (in production).
    /// </summary>
    public required string Host { get; init; }

    /// <summary>
    /// Gets the port number of the SMTP server.
    /// e.g., 1025 (for Mailpit) or 587 (for production services).
    /// </summary>
    public int Port { get; init; }

    /// <summary>
    /// Gets a value indicating whether to use SSL/TLS for the connection.
    /// </summary>
    public bool UseSsl { get; init; }

    /// <summary>
    /// Gets the username for SMTP authentication. Can be null for unauthenticated relays.
    /// </summary>
    public string? Username { get; init; }

    /// <summary>
    /// Gets the password for SMTP authentication. Can be null for unauthenticated relays.
    /// </summary>
    public string? Password { get; init; }

    /// <summary>
    /// Gets the email address to be used in the "From" field of outgoing emails.
    /// e.g., "noreply@meetingsystem.com"
    /// </summary>
    public required string FromAddress { get; init; }

    /// <summary>
    /// Gets the display name to be used in the "From" field of outgoing emails.
    /// e.g., "MeetingSystem Notifications"
    /// </summary>
    public required string FromName { get; init; }
}