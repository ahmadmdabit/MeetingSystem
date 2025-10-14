namespace MeetingSystem.Business.Configuration;

/// <summary>
/// Contains configuration settings for the Hangfire background job processing system.
/// </summary>
public class HangfireSettings
{
    /// <summary>
    /// The name of the configuration section in appsettings.json.
    /// </summary>
    public const string SectionName = "Hangfire";

    /// <summary>
    /// The number of worker threads Hangfire should use. Defaults to 20.
    /// </summary>
    public int WorkerCount { get; init; } = 20;

    /// <summary>
    /// The name of the role required to access the Hangfire Dashboard.
    /// </summary>
    public required string DashboardAdminRole { get; init; }

    /// <summary>
    /// The number of days after cancellation before a meeting is eligible for permanent deletion.
    /// </summary>
    public double CleanupThresholdDays { get; init; } = 30;

    /// <summary>
    /// The CRON expression for scheduling the recurring meeting cleanup job.
    /// </summary>
    public required string CleanupJobCronExpression { get; init; }

    /// <summary>
    /// Gets the default configuration settings for Hangfire. This property provides
    /// a baseline configuration with sensible defaults that can be used as a starting
    /// point for custom configurations or as fallback values.
    /// </summary>
    /// <value>
    /// A new HangfireSettings instance with the following default values:
    /// - WorkerCount: 20 threads
    /// - DashboardAdminRole: "Admin"
    /// - CleanupThresholdDays: 30 days
    /// - CleanupJobCronExpression: "0 0 * * *" (daily at midnight UTC)
    /// </value>
    /// <remarks>
    /// These defaults ensure optimal performance for typical use cases while
    /// maintaining system security through role-based access control. The default
    /// cleanup schedule runs daily at midnight UTC to remove old cancelled meetings
    /// after the 30-day retention period.
    /// </remarks>
    public static HangfireSettings Default => new()
    {
        WorkerCount = 20,
        DashboardAdminRole = "Admin",
        CleanupThresholdDays = 30,
        CleanupJobCronExpression = "0 0 * * *"
    };
}