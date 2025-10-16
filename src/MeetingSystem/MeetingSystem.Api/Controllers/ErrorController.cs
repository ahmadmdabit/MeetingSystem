using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace MeetingSystem.Api.Controllers;

/// <summary>
/// Provides a centralized endpoint for handling unhandled exceptions.
/// </summary>
[ApiController]
[ApiExplorerSettings(IgnoreApi = true)] // Hide this from api meta tools documentation
public class ErrorController : ControllerBase
{
    private readonly ILogger<ErrorController> _logger;

    public ErrorController(ILogger<ErrorController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// The action that is invoked by the Exception Handler Middleware.
    /// </summary>
    [Route("/error")]
    public IActionResult HandleError([FromServices] IHostEnvironment hostEnvironment)
    {
        // Retrieve the exception details from the HttpContext
        var exceptionHandlerFeature = HttpContext.Features.Get<IExceptionHandlerFeature>();
        if (exceptionHandlerFeature == null)
        {
            // This should not happen, but it's a safe fallback.
            return Problem(
                title: "An unexpected error occurred.",
                statusCode: StatusCodes.Status500InternalServerError);
        }

        var exception = exceptionHandlerFeature.Error;

        // Log the full exception details for debugging and auditing
        _logger.LogError(exception, "An unhandled exception occurred: {Message}", exception.Message);

        // In a development environment, return detailed information.
        if (hostEnvironment.IsDevelopment())
        {
            return Problem(
                detail: exception.StackTrace,
                title: exception.Message,
                statusCode: StatusCodes.Status500InternalServerError
            );
        }

        // In a production environment, return a generic, safe error message.
        return Problem(
            title: "An unexpected internal server error occurred. Please try again later.",
            statusCode: StatusCodes.Status500InternalServerError
        );
    }
}