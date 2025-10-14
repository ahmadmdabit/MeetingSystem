using System.Threading.Tasks;
using MeetingSystem.Business;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MeetingSystem.Api.Controllers;

/// <summary>
/// Provides administrative endpoints for managing the system.
/// </summary>
[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
[Produces("application/json")]
public class AdminController : ControllerBase
{
    private readonly IAdminService _adminService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminController"/> class.
    /// </summary>
    public AdminController(IAdminService adminService)
    {
        _adminService = adminService;
    }

    /// <summary>
    /// Manually triggers a background job to clean up old meetings.
    /// </summary>
    [HttpPost("jobs/trigger-cleanup")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> TriggerCleanupJob()
    {
        await _adminService.TriggerMeetingCleanupJobAsync();
        return Accepted(new { Message = "Meeting cleanup job has been successfully triggered." });
    }
}
