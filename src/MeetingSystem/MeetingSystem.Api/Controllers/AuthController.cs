using System.Security.Claims;
using MeetingSystem.Business;
using MeetingSystem.Business.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace MeetingSystem.Api.Controllers;

/// <summary>
/// Manages user authentication, registration, and session control.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// Registers a new user in the system.
    /// </summary>
    /// <param name="dto">The user registration data.</param>
    /// <param name="cancellationToken">A token to cancel the operation if the client disconnects.</param>
    [HttpPost("register")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromForm] RegisterUserDto dto, CancellationToken cancellationToken)
    {
        var (success, message) = await _authService.RegisterAsync(dto, true, cancellationToken).ConfigureAwait(false);
        if (!success)
        {
            return BadRequest(new { Message = message });
        }
        return Ok(new { Message = message });
    }

    /// <summary>
    /// Authenticates a user and provides a JWT.
    /// </summary>
    /// <param name="dto">The user's login credentials.</param>
    /// <param name="cancellationToken">A token to cancel the operation if the client disconnects.</param>
    [HttpPost("login")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginDto dto, CancellationToken cancellationToken)
    {
        var (success, token) = await _authService.LoginAsync(dto, cancellationToken).ConfigureAwait(false);
        if (!success)
        {
            return Unauthorized(new { Message = token });
        }
        return Ok(new { Token = token });
    }

    /// <summary>
    /// Revokes the current user's JWT, effectively logging them out.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation if the client disconnects.</param>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var jti = User.FindFirstValue(JwtRegisteredClaimNames.Jti);
        if (jti == null)
        {
            return BadRequest("Token does not contain a JTI claim.");
        }

        await _authService.LogoutAsync(jti, cancellationToken).ConfigureAwait(false);
        return Ok(new { Message = "Successfully logged out." });
    }
}