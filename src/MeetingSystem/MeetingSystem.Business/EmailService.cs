using MailKit.Net.Smtp;
using MeetingSystem.Business.Configuration;
using Microsoft.Extensions.Options;
using MimeKit;

namespace MeetingSystem.Business;

/// <summary>
/// Defines the contract for a service that handles sending emails.
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Sends a welcome email to a newly registered user using an HTML template.
    /// </summary>
    /// <param name="userEmail">The recipient's email address.</param>
    /// <param name="userName">The recipient's name for personalization.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SendWelcomeEmailAsync(string userEmail, string userName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a reminder email for an upcoming meeting using an HTML template.
    /// </summary>
    /// <param name="userEmail">The recipient's email address.</param>
    /// <param name="meetingName">The name of the meeting.</param>
    /// <param name="meetingStart">The UTC start time of the meeting.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SendMeetingReminderAsync(string userEmail, string meetingName, DateTime meetingStart, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends an email with an HTML body and a single attachment.
    /// </summary>
    /// <param name="to">The recipient's email address.</param>
    /// <param name="subject">The subject of the email.</param>
    /// <param name="htmlBody">The HTML content of the email body.</param>
    /// <param name="attachmentFileName">The name of the file to attach.</param>
    /// <param name="attachmentStream">The stream containing the file's content.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SendEmailWithAttachmentAsync(string to, string subject, string htmlBody, string attachmentFileName, Stream attachmentStream, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implements the <see cref="IEmailService"/> using MailKit to send richly formatted HTML emails with attachments.
/// </summary>
public class EmailService : IEmailService
{
    private readonly SmtpSettings _smtpSettings;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailService"/> class.
    /// </summary>
    /// <param name="smtpSettings">The strongly-typed SMTP configuration options.</param>
    public EmailService(IOptions<SmtpSettings> smtpSettings)
    {
        _smtpSettings = smtpSettings.Value;
    }

    /// <inheritdoc />
    public Task SendWelcomeEmailAsync(string userEmail, string userName, CancellationToken cancellationToken = default)
    {
        var subject = "Welcome to MeetingSystem!";
        // In a real application, this HTML would be loaded from a Razor template, Scriban, or a file.
        var htmlBody = $"<h1>Welcome, {userName}!</h1><p>Thank you for registering with MeetingSystem.</p>";
        return SendHtmlEmailAsync(userEmail, subject, htmlBody, cancellationToken);
    }

    /// <inheritdoc />
    public Task SendMeetingReminderAsync(string userEmail, string meetingName, DateTime meetingStart, CancellationToken cancellationToken = default)
    {
        var subject = $"Reminder: {meetingName}";
        var htmlBody = $"<p>This is a reminder that your meeting, '<b>{meetingName}</b>', is scheduled to start at {meetingStart:g} UTC.</p>";
        return SendHtmlEmailAsync(userEmail, subject, htmlBody, cancellationToken);
    }

    /// <inheritdoc />
    public Task SendEmailWithAttachmentAsync(string to, string subject, string htmlBody, string attachmentFileName, Stream attachmentStream, CancellationToken cancellationToken = default)
    {
        var bodyBuilder = new BodyBuilder { HtmlBody = htmlBody };
        bodyBuilder.Attachments.Add(attachmentFileName, attachmentStream, cancellationToken);
        
        return SendEmailAsync(to, subject, bodyBuilder.ToMessageBody(), cancellationToken);
    }

    /// <summary>
    /// Constructs and sends an email with an HTML body.
    /// </summary>
    private Task SendHtmlEmailAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken)
    {
        var bodyBuilder = new BodyBuilder { HtmlBody = htmlBody };
        return SendEmailAsync(to, subject, bodyBuilder.ToMessageBody(), cancellationToken);
    }

    /// <summary>
    /// The core method for constructing and sending an email using the configured SMTP settings.
    /// </summary>
    private async Task SendEmailAsync(string to, string subject, MimeEntity body, CancellationToken cancellationToken)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_smtpSettings.FromName, _smtpSettings.FromAddress));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = body;

        using var client = new SmtpClient();
        
        await client.ConnectAsync(_smtpSettings.Host, _smtpSettings.Port, _smtpSettings.UseSsl, cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(_smtpSettings.Username))
        {
            await client.AuthenticateAsync(_smtpSettings.Username, _smtpSettings.Password, cancellationToken).ConfigureAwait(false);
        }

        await client.SendAsync(message, cancellationToken).ConfigureAwait(false);
        await client.DisconnectAsync(true, cancellationToken).ConfigureAwait(false);
    }
}