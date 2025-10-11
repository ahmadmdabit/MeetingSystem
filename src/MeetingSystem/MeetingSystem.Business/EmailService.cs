using System.Threading;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Configuration;
using MimeKit;

namespace MeetingSystem.Business;

/// <summary>
/// Defines the contract for a service that handles sending emails.
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Sends a welcome email to a newly registered user.
    /// </summary>
    /// <param name="userEmail">The recipient's email address.</param>
    /// <param name="userName">The recipient's name for personalization.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SendWelcomeEmailAsync(string userEmail, string userName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a reminder email for an upcoming meeting.
    /// </summary>
    /// <param name="userEmail">The recipient's email address.</param>
    /// <param name="meetingName">The name of the meeting.</param>
    /// <param name="meetingStart">The UTC start time of the meeting.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SendMeetingReminderAsync(string userEmail, string meetingName, DateTime meetingStart, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implements the <see cref="IEmailService"/> using MailKit to send emails via an SMTP server.
/// </summary>
public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailService"/> class.
    /// </summary>
    /// <param name="configuration">The application's configuration provider for accessing SMTP settings.</param>
    public EmailService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <inheritdoc />
    public async Task SendWelcomeEmailAsync(string userEmail, string userName, CancellationToken cancellationToken = default)
    {
        var subject = "Welcome to MeetingSystem!";
        var body = $"Hi {userName},\n\nThank you for registering with MeetingSystem.";
        await SendEmailAsync(userEmail, subject, body, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SendMeetingReminderAsync(string userEmail, string meetingName, DateTime meetingStart, CancellationToken cancellationToken = default)
    {
        var subject = $"Reminder: {meetingName}";
        var body = $"This is a reminder that your meeting, '{meetingName}', is scheduled to start at {meetingStart:g} UTC.";
        await SendEmailAsync(userEmail, subject, body, cancellationToken).ConfigureAwait(false);
    }

    private async Task SendEmailAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_configuration["Smtp:FromName"], _configuration["Smtp:FromAddress"]));
        message.To.Add(new MailboxAddress("", to));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        using var client = new SmtpClient();

        var host = _configuration["Smtp:Host"];
        if (!int.TryParse(_configuration["Smtp:Port"], out int port)) { port = 1025; }
        if (!bool.TryParse(_configuration["Smtp:SSL"], out bool ssl)) { ssl = false; }
        
        await client.ConnectAsync(host, port, ssl, cancellationToken).ConfigureAwait(false);

        var username = _configuration["Smtp:Username"];
        var password = _configuration["Smtp:Password"];
        if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
        {
            await client.AuthenticateAsync(username, password, cancellationToken).ConfigureAwait(false);
        }

        await client.SendAsync(message, cancellationToken).ConfigureAwait(false);
        await client.DisconnectAsync(true, cancellationToken).ConfigureAwait(false);
    }
}