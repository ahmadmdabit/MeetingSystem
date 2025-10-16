using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MeetingSystem.Api.Filters;

/// <summary>
/// A filter factory that creates a <see cref="RequestSizeLimitAttribute"/> with a limit
/// read from the application's configuration.
/// </summary>
public class RequestSizeLimitFilterFactory : Attribute, IFilterFactory
{
    private readonly string _configurationKey;

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestSizeLimitFilterFactory"/> class.
    /// </summary>
    /// <param name="configurationKey">The key in the configuration file to read the max file size from (e.g., "Minio:MaxFileSize").</param>
    public RequestSizeLimitFilterFactory(string configurationKey)
    {
        _configurationKey = configurationKey;
    }

    /// <summary>
    /// Gets a value that indicates if the filter created by this factory is reusable.
    /// </summary>
    public bool IsReusable => false;

    /// <summary>
    /// Creates an instance of the executable filter.
    /// </summary>
    /// <param name="serviceProvider">The request's service provider.</param>
    /// <returns>An instance of the filter.</returns>
    public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
    {
        // Resolve the IConfiguration service
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();

        // Read the value from configuration
        if (!long.TryParse(configuration[_configurationKey], out var maxFileSize) || maxFileSize <= 0)
        {
            // Fallback to a default value if not configured or invalid
            maxFileSize = 52428800; // 50 MB
        }

        // Create and return the actual filter with the runtime-configured value
        return new RequestSizeLimitAttribute((int)maxFileSize);
    }
}