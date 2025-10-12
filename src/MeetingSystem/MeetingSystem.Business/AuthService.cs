using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Hangfire;
using MeetingSystem.Context;
using MeetingSystem.Business.Dtos;
using MeetingSystem.Model;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace MeetingSystem.Business;

/// <summary>
/// Defines the contract for handling user authentication and registration.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Registers a new user in the system with a default 'User' role.
    /// </summary>
    /// <param name="dto">The data transfer object containing user registration information.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A tuple indicating success and a corresponding message.</returns>
    Task<(bool Success, string Message)> RegisterAsync(RegisterUserDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Authenticates a user and generates a JWT containing their roles.
    /// </summary>
    /// <param name="dto">The data transfer object containing user login credentials.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A tuple indicating success and the generated JWT, or an error message on failure.</returns>
    Task<(bool Success, string Token)> LoginAsync(LoginDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes a JWT by adding its JTI claim to the blacklist.
    /// </summary>
    /// <param name="jti">The JTI (JWT ID) claim of the token to be revoked.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task LogoutAsync(string jti, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implements the <see cref="IAuthService"/> to manage user registration, login, and logout operations,
/// including role-based authorization claims.
/// </summary>
public class AuthService : IAuthService
{
    private readonly IConfiguration _configuration;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly IEmailService _emailService;
    private readonly IProfilePictureService _profilePictureService;
    private readonly ILogger<AuthService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthService"/> class.
    /// </summary>
    public AuthService(IUnitOfWork unitOfWork,
        IPasswordHasher<User> passwordHasher,
        IBackgroundJobClient backgroundJobClient,
        IEmailService emailService,
        IProfilePictureService profilePictureService,
        IConfiguration configuration,
        ILogger<AuthService> logger)
    {
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _backgroundJobClient = backgroundJobClient;
        _emailService = emailService;
        _profilePictureService = profilePictureService;
        _configuration = configuration;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<(bool Success, string Message)> RegisterAsync(RegisterUserDto dto, CancellationToken cancellationToken = default)
    {
        await _unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var userExists = await _unitOfWork.Users.Find(u => u.Email == dto.Email).AnyAsync(cancellationToken).ConfigureAwait(false);
            if (userExists)
            {
                return (false, "Email is already registered.");
            }

            var user = new User
            {
                Id = Guid.NewGuid(),
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                Email = dto.Email,
                Phone = dto.Phone,
                PasswordHash = string.Empty,
            };

            user.PasswordHash = _passwordHasher.HashPassword(user, dto.Password);

            _unitOfWork.Users.Add(user);
            // Save the user first to ensure the user record exists before we try to attach a file to it.
            await _unitOfWork.CompleteAsync(cancellationToken).ConfigureAwait(false);

            // Delegate profile picture handling to the dedicated service.
            if (dto.ProfilePicture != null)
            {
                try
                {
                    await _profilePictureService.SetAsync(user.Id, dto.ProfilePicture, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to upload profile picture for new user {UserId}", user.Id);
                    // Currently, this is not fatal error. We'll let the registration succeed without a picture.
                }
            }

            var userRole = await _unitOfWork.Roles.Find(r => r.Name == "User").FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            if (userRole == null)
            {
                _logger.LogError("Default 'User' role not found in the database. Cannot assign role to new user.");
                await _unitOfWork.RollbackTransactionAsync(cancellationToken).ConfigureAwait(false);
                return (false, "System configuration error: Default role not found.");
            }
            
            _unitOfWork.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = userRole.Id });
            
            await _unitOfWork.CommitTransactionAsync(cancellationToken).ConfigureAwait(false);

            _backgroundJobClient.Enqueue<IEmailService>(
                emailService => emailService.SendWelcomeEmailAsync(user.Email, user.FirstName, CancellationToken.None));

            return (true, "User registered successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred during user registration for email {Email}.", dto.Email);
            await _unitOfWork.RollbackTransactionAsync(cancellationToken).ConfigureAwait(false);
            return (false, "An unexpected error occurred during registration.");
        }
    }

    /// <inheritdoc />
    public async Task<(bool Success, string Token)> LoginAsync(LoginDto dto, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Users.Find(u => u.Email == dto.Email).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (user == null) return (false, "Invalid credentials.");

        var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, dto.Password);
        if (result == PasswordVerificationResult.Failed) return (false, "Invalid credentials.");

        var token = await GenerateJwtToken(user, cancellationToken).ConfigureAwait(false);
        return (true, token);
    }

    /// <inheritdoc />
    public async Task LogoutAsync(string jti, CancellationToken cancellationToken = default)
    {
        var revokedToken = new RevokedToken { Jti = jti };
        _unitOfWork.RevokedTokens.Add(revokedToken);
        await _unitOfWork.CompleteAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Generates a signed JWT for the specified user, including their assigned roles as claims.
    /// </summary>
    /// <param name="user">The user for whom to generate the token.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task that results in a signed JWT string.</returns>
    private async Task<string> GenerateJwtToken(User user, CancellationToken cancellationToken = default)
    {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var roles = await _unitOfWork.UserRoles
            .Find(ur => ur.UserId == user.Id)
            .Join(_unitOfWork.Roles.GetAll(), ur => ur.RoleId, r => r.Id, (ur, r) => r.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        if (!int.TryParse(_configuration["Jwt:ExpiresIn"], out int expiresIn))
        {
            expiresIn = 360;
        }

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiresIn),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}