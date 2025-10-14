using System;
using System.Linq;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using FluentAssertions;
using Hangfire;
using MeetingSystem.Business;
using MeetingSystem.Context;
using MeetingSystem.Business.Dtos;
using MeetingSystem.Model;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Hangfire.Common;
using Microsoft.AspNetCore.Http;

namespace MeetingSystem.Business.Tests;

/// <summary>
/// Contains unit tests for the AuthService class.
/// These tests use a real MSSQL database via Testcontainers for high-fidelity data access testing.
/// </summary>
[TestFixture]
public class AuthServiceTests
{
    private MeetingSystemDbContext _dbContext;
    private IUnitOfWork _unitOfWork;
    private Mock<IPasswordHasher<User>> _passwordHasherMock;
    private Mock<IBackgroundJobClient> _backgroundJobClientMock;
    private Mock<IEmailService> _emailServiceMock;
    private Mock<IProfilePictureService> _profilePictureServiceMock;
    private Mock<IConfiguration> _configurationMock;
    private Mock<ILogger<AuthService>> _loggerMock;
    private AuthService _authService;

    /// <summary>
    /// Runs before each individual test.
    /// It creates a fresh, clean database schema and initializes all services.
    /// </summary>
    [SetUp]
    public async Task SetUp()
    {
        // 1. Configure DbContext to use the connection string from the GlobalSetup.
        var options = new DbContextOptionsBuilder<MeetingSystemDbContext>()
            .UseSqlServer(GlobalSetup.ConnectionString)
            .Options;

        _dbContext = new MeetingSystemDbContext(options);
        
        // 2. This cleanup is now MORE IMPORTANT THAN EVER.
        // It ensures each test is isolated from the others.
        await _dbContext.Database.EnsureDeletedAsync();
        await _dbContext.Database.MigrateAsync();

        _unitOfWork = new UnitOfWork(_dbContext, Mock.Of<ILogger<UnitOfWork>>());
        
        _passwordHasherMock = new Mock<IPasswordHasher<User>>();
        _backgroundJobClientMock = new Mock<IBackgroundJobClient>();
        _emailServiceMock = new Mock<IEmailService>();
        _profilePictureServiceMock = new Mock<IProfilePictureService>();
        _configurationMock = new Mock<IConfiguration>();
        _loggerMock = new Mock<ILogger<AuthService>>();
        SetupDefaultConfiguration();

        _authService = new AuthService(
            _unitOfWork,
            _passwordHasherMock.Object,
            _backgroundJobClientMock.Object,
            _emailServiceMock.Object,
            _profilePictureServiceMock.Object,
            _configurationMock.Object,
            _loggerMock.Object);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _unitOfWork.DisposeAsync();
        await _dbContext.DisposeAsync();
    }

    private void SetupDefaultConfiguration()
    {
        _configurationMock.Setup(c => c["Jwt:Key"]).Returns("ThisIsASecretKeyForJwtTokenGenerationWithAtLeast32Characters!");
        _configurationMock.Setup(c => c["Jwt:Issuer"]).Returns("MeetingSystem");
        _configurationMock.Setup(c => c["Jwt:Audience"]).Returns("MeetingSystemUsers");
        _configurationMock.Setup(c => c["Jwt:ExpiresIn"]).Returns("360");
    }

    #region RegisterAsync Tests

    /// <summary>
    /// Verifies that RegisterAsync fails if the email already exists in the database.
    /// </summary>
    [Test]
    public async Task RegisterAsync_WhenEmailAlreadyExists_ReturnsFailure()
    {
        // Arrange: Seed the database with an existing user.
        var existingUser = new User { Id = Guid.NewGuid(), Email = "existing@example.com", FirstName = "John", LastName = "Doe", Phone = "123", PasswordHash = "hash" };
        _dbContext.Users.Add(existingUser);
        await _dbContext.SaveChangesAsync();

        var dto = new RegisterUserDto("Jane", "Smith", "existing@example.com", "456", "Password123!", null);

        // Act
        var (success, message) = await _authService.RegisterAsync(dto);

        // Assert
        success.Should().BeFalse();
        message.Should().Be("Email is already registered.");
    }

    /// <summary>
    /// Verifies a successful user registration, including database persistence and role assignment.
    /// </summary>
    [Test]
    public async Task RegisterAsync_WhenUserIsNew_CreatesUserAndAssignsRoleInDatabase()
    {
        // Arrange
        var dto = new RegisterUserDto("Jane", "Smith", "newuser@example.com", "9876543210", "Password123!", null);
        await SeedRoles(); // Ensure the "User" role exists
        _passwordHasherMock.Setup(p => p.HashPassword(It.IsAny<User>(), dto.Password)).Returns("hashed_password");

        // Act
        var (success, message) = await _authService.RegisterAsync(dto);

        // Assert
        success.Should().BeTrue();
        message.Should().Be("User registered successfully.");
        
        // Verify directly against the database
        var createdUser = await _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == dto.Email);
        createdUser.Should().NotBeNull();
        createdUser!.FirstName.Should().Be(dto.FirstName);

        var roleAssignment = await _dbContext.UserRoles.AsNoTracking().FirstOrDefaultAsync(ur => ur.UserId == createdUser.Id);
        roleAssignment.Should().NotBeNull();

        // Verify the underlying 'Create' method on IBackgroundJobClient instead of the 'Enqueue' extension method.
        _backgroundJobClientMock.Verify(
            client => client.Create(
                // We can use It.IsAny<Job>() because the exact job details are complex to match.
                // The important part is that *a* job was created.
                It.Is<Job>(job =>
                    job.Method.DeclaringType == typeof(IEmailService) &&
                    job.Method.Name == nameof(IEmailService.SendWelcomeEmailAsync)
                ),
                // Check that it's being added to the "Enqueued" state.
                It.IsAny<Hangfire.States.EnqueuedState>()
            ),
            Times.Once // Ensure it was called exactly once.
        );
    }

    /// <summary>
    /// Verifies that registration fails if the required "User" role has not been seeded.
    /// </summary>
    [Test]
    public async Task RegisterAsync_WhenDefaultRoleNotFound_ReturnsFailureAndRollsBack()
    {
        // Arrange
        var dto = new RegisterUserDto("Jane", "Smith", "newuser@example.com", "9876543210", "Password123!", null);
        // Note: We do NOT seed any roles for this test.
        _passwordHasherMock.Setup(p => p.HashPassword(It.IsAny<User>(), dto.Password)).Returns("hashed_password");

        // Act
        var (success, message) = await _authService.RegisterAsync(dto);

        // Assert
        success.Should().BeFalse();
        message.Should().Be("System configuration error: Default role not found.");

        // Verify that the transaction was rolled back and no user was created.
        (await _dbContext.Users.CountAsync()).Should().Be(0);
    }
    
    /// <summary>
    /// Verifies that the main catch block in RegisterAsync is triggered and rolls back the transaction
    /// when a database constraint violation occurs.
    /// </summary>
    [Test]
    public async Task RegisterAsync_WhenDbConstraintFails_RollsBackAndReturnsFailure()
    {
        // Arrange
        // Create a DTO with an email that is too long for the database schema constraint.
        var invalidEmail = new string('a', 300) + "@example.com";
        var dto = new RegisterUserDto("Jane", "Smith", invalidEmail, "9876543210", "Password123!", null);
        await SeedRoles();
        _passwordHasherMock.Setup(p => p.HashPassword(It.IsAny<User>(), dto.Password)).Returns("hashed_password");

        // Act
        var (success, message) = await _authService.RegisterAsync(dto);

        // Assert
        success.Should().BeFalse();
        message.Should().Be("An unexpected error occurred during registration.");
        
        // Verify no user was created due to the rollback
        (await _dbContext.Users.CountAsync()).Should().Be(0);
        
        // Verify the error was logged
        _loggerMock.Verify(
            logger => logger.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("An error occurred during user registration")),
                It.IsAny<DbUpdateException>(), // Expect a specific EF Core exception
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Verifies that the non-fatal catch block for profile picture uploads is correctly triggered and logged.
    /// </summary>
    [Test]
    public async Task RegisterAsync_WhenProfilePictureServiceThrows_LogsErrorAndSucceeds()
    {
        // Arrange
        var profilePictureMock = new Mock<IFormFile>();
        var dto = new RegisterUserDto("Jane", "Smith", "newuser@example.com", "9876543210", "Password123!", profilePictureMock.Object);
        await SeedRoles();
        _passwordHasherMock.Setup(p => p.HashPassword(It.IsAny<User>(), dto.Password)).Returns("hashed_password");

        _profilePictureServiceMock.Setup(s => s.SetAsync(It.IsAny<Guid>(), dto.ProfilePicture!, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("MinIO upload failed"));

        // Act
        var (success, message) = await _authService.RegisterAsync(dto);

        // Assert
        success.Should().BeTrue();
        message.Should().Be("User registered successfully.");

        _loggerMock.Verify(
            logger => logger.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to upload profile picture")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Verifies that GenerateJwtToken uses the default expiration when the configuration is invalid.
    /// </summary>
    [Test]
    public async Task GenerateJwtToken_WhenExpiresInIsInvalid_UsesDefaultExpiration()
    {
        // Arrange
        _configurationMock.Setup(c => c["Jwt:ExpiresIn"]).Returns("not-a-number");

        var user = new User { Id = Guid.NewGuid(), Email = "test@example.com", PasswordHash = "hash", FirstName = "Test", LastName = "User", Phone = "123" };
        var dto = new LoginDto(user.Email, "password");
        
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();
        
        _passwordHasherMock.Setup(p => p.VerifyHashedPassword(user, user.PasswordHash, dto.Password))
            .Returns(PasswordVerificationResult.Success);

        var beforeLogin = DateTime.UtcNow;

        // Act
        var (success, tokenString) = await _authService.LoginAsync(dto);

        // Assert
        success.Should().BeTrue();
        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(tokenString);

        var expectedExpiry = beforeLogin.AddMinutes(360);
        token.ValidTo.Should().BeCloseTo(expectedExpiry, TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// Verifies the successful registration path where a profile picture is provided and successfully uploaded.
    /// This test covers the successful completion of the inner try-catch block.
    /// </summary>
    [Test]
    public async Task RegisterAsync_WithSuccessfulProfilePictureUpload_Succeeds()
    {
        // Arrange
        var profilePictureMock = new Mock<IFormFile>();
        var dto = new RegisterUserDto("Picture", "User", "pictureuser@example.com", "111222333", "Password123!", profilePictureMock.Object);
        await SeedRoles();
        _passwordHasherMock.Setup(p => p.HashPassword(It.IsAny<User>(), dto.Password)).Returns("hashed_password");

        // Setup the profile picture service to succeed
        _profilePictureServiceMock
            .Setup(s => s.SetAsync(It.IsAny<Guid>(), dto.ProfilePicture!, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, null as string));

        // Act
        var (success, message) = await _authService.RegisterAsync(dto);

        // Assert
        success.Should().BeTrue();
        message.Should().Be("User registered successfully.");

        // Verify the service was called
        _profilePictureServiceMock.Verify(
            s => s.SetAsync(It.IsAny<Guid>(), profilePictureMock.Object, It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }    
    
    #endregion

    #region LoginAsync Tests

    /// <summary>
    /// Verifies that login fails for a non-existent user.
    /// </summary>
    [Test]
    public async Task LoginAsync_WhenUserNotFound_ReturnsFailure()
    {
        // Arrange
        var dto = new LoginDto("nonexistent@example.com", "Password123!");

        // Act
        var (success, token) = await _authService.LoginAsync(dto);

        // Assert
        success.Should().BeFalse();
        token.Should().Be("Invalid credentials.");
    }

    /// <summary>
    /// Verifies a successful login returns a valid JWT with correct claims.
    /// </summary>
    [Test]
    public async Task LoginAsync_WhenCredentialsAreValid_ReturnsSuccessWithTokenContainingRoles()
    {
        // Arrange: Seed a user and assign them the "Admin" role.
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, Email = "admin@example.com", PasswordHash = "correct_hash", FirstName = "Admin", LastName = "User", Phone = "123" };
        var role = new Role { Id = Guid.NewGuid(), Name = "Admin" };
        var userRole = new UserRole { UserId = userId, RoleId = role.Id };
        _dbContext.Users.Add(user);
        _dbContext.Roles.Add(role);
        _dbContext.UserRoles.Add(userRole);
        await _dbContext.SaveChangesAsync();

        var dto = new LoginDto("admin@example.com", "Password123!");

        _passwordHasherMock.Setup(p => p.VerifyHashedPassword(user, user.PasswordHash, dto.Password))
            .Returns(PasswordVerificationResult.Success);

        // Act
        var (success, tokenString) = await _authService.LoginAsync(dto);

        // Assert
        success.Should().BeTrue();
        
        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(tokenString);
        
        token.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == userId.ToString());
        token.Claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == "Admin");
    }

    /// Verifies that login succeeds when the password is correct but the hashing algorithm is outdated
    /// (indicated by PasswordVerificationResult.SuccessRehashNeeded).
    /// </summary>
    [Test]
    public async Task LoginAsync_WhenPasswordVerificationReturnsSuccessRehashNeeded_ReturnsSuccessWithToken()
    {
        // Arrange
        var user = new User { Id = Guid.NewGuid(), Email = "rehash@example.com", PasswordHash = "old_format_hash", FirstName = "Test", LastName = "User", Phone = "123" };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var dto = new LoginDto(user.Email, "Password123!");

        // Setup the mock password hasher to return SuccessRehashNeeded
        _passwordHasherMock
            .Setup(p => p.VerifyHashedPassword(user, user.PasswordHash, dto.Password))
            .Returns(PasswordVerificationResult.SuccessRehashNeeded);

        // Act
        var (success, token) = await _authService.LoginAsync(dto);

        // Assert
        success.Should().BeTrue();
        token.Should().NotBeNullOrEmpty();
        
        var handler = new JwtSecurityTokenHandler();
        var decodedToken = handler.ReadJwtToken(token);
        decodedToken.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == user.Id.ToString());
    }

    #endregion

    #region LogoutAsync Tests

    /// <summary>
    /// Verifies that LogoutAsync correctly persists the revoked token JTI to the database.
    /// </summary>
    [Test]
    public async Task LogoutAsync_AddsTokenJtiToDatabase()
    {
        // Arrange
        var jti = Guid.NewGuid().ToString();

        // Act
        await _authService.LogoutAsync(jti);

        // Assert: Verify directly against the database.
        var revokedToken = await _dbContext.RevokedTokens.AsNoTracking().FirstOrDefaultAsync(rt => rt.Jti == jti);
        revokedToken.Should().NotBeNull();
        revokedToken!.Jti.Should().Be(jti);
    }

    #endregion

    #region Profile Tests

    [Test]
    public async Task GetCurrentUserProfileAsync_WhenUserExists_ReturnsProfile()
    {
        // Arrange
        var user = await CreateUserAsync("profile@test.com");

        // Act
        var profile = await _authService.GetCurrentUserProfileAsync(user.Id);

        // Assert
        profile.Should().NotBeNull();
        profile!.Id.Should().Be(user.Id);
        profile.Email.Should().Be(user.Email);
    }

    [Test]
    public async Task GetCurrentUserProfileAsync_WhenUserDoesNotExist_ReturnsNull()
    {
        // Arrange
        var nonExistentUserId = Guid.NewGuid();

        // Act
        var profile = await _authService.GetCurrentUserProfileAsync(nonExistentUserId);

        // Assert
        profile.Should().BeNull();
    }

    [Test]
    public async Task UpdateCurrentUserProfileAsync_WhenUserExists_UpdatesProfile()
    {
        // Arrange
        var user = await CreateUserAsync("update@test.com");
        var dto = new UpdateUserProfileDto("UpdatedFirst", "UpdatedLast", "555-1234");

        // Act
        var (success, errorMessage) = await _authService.UpdateCurrentUserProfileAsync(user.Id, dto);

        // Assert
        success.Should().BeTrue();
        errorMessage.Should().BeNull();

        var updatedUser = await _dbContext.Users.FindAsync(user.Id);
        updatedUser!.FirstName.Should().Be("UpdatedFirst");
        updatedUser.LastName.Should().Be("UpdatedLast");
        updatedUser.Phone.Should().Be("555-1234");
    }

    [Test]
    public async Task UpdateCurrentUserProfileAsync_WhenUserDoesNotExist_ReturnsFailure()
    {
        // Arrange
        var nonExistentUserId = Guid.NewGuid();
        var dto = new UpdateUserProfileDto("First", "Last", "123");

        // Act
        var (success, errorMessage) = await _authService.UpdateCurrentUserProfileAsync(nonExistentUserId, dto);

        // Assert
        success.Should().BeFalse();
        errorMessage.Should().Be("User not found.");
    }

    #endregion

    #region Helper Methods

    private async Task<User> CreateUserAsync(string email)
    {
        var user = new User { Id = Guid.NewGuid(), Email = email, FirstName = "Test", LastName = "User", Phone = "123", PasswordHash = "hash" };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();
        return user;
    }

    /// <summary>
    /// Helper to seed the "Admin" and "User" roles into the test database.
    /// </summary>
    private async Task SeedRoles()
    {
        _dbContext.Roles.AddRange(
            new Role { Id = Guid.NewGuid(), Name = "Admin" },
            new Role { Id = Guid.NewGuid(), Name = "User" }
        );
        await _dbContext.SaveChangesAsync();
    }

    #endregion
}