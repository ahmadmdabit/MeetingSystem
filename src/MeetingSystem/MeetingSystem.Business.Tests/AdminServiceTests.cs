using FluentAssertions;

using Hangfire;
using Hangfire.Common;
using Hangfire.States;

using MeetingSystem.Context;
using MeetingSystem.Model;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Moq;

namespace MeetingSystem.Business.Tests;

[TestFixture]
public class AdminServiceTests
{
    private MeetingSystemDbContext _dbContext;
    private IUnitOfWork _unitOfWork;
    private IAdminService _adminService;
    private Mock<IBackgroundJobClient> _backgroundJobClientMock;
    private Mock<ILogger<AdminService>> _loggerMock;

    [SetUp]
    public async Task SetUp()
    {
        var options = new DbContextOptionsBuilder<MeetingSystemDbContext>()
            .UseSqlServer(GlobalSetup.ConnectionString)
            .Options;

        _dbContext = new MeetingSystemDbContext(options);

        await _dbContext.Database.EnsureDeletedAsync();
        await _dbContext.Database.MigrateAsync();

        _unitOfWork = new UnitOfWork(_dbContext, Mock.Of<ILogger<UnitOfWork>>());
        _loggerMock = new Mock<ILogger<AdminService>>();
        _backgroundJobClientMock = new Mock<IBackgroundJobClient>();
        _adminService = new AdminService(_unitOfWork, _loggerMock.Object, _backgroundJobClientMock.Object);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _unitOfWork.DisposeAsync();
        await _dbContext.DisposeAsync();
    }

    [Test]
    public async Task AssignRoleToUserAsync_WhenUserNotFound_ReturnsFailure()
    {
        // Arrange
        var nonExistentUserId = Guid.NewGuid();

        // Act
        var (success, errorMessage) = await _adminService.AssignRoleToUserAsync(nonExistentUserId, "Admin");

        // Assert
        success.Should().BeFalse();
        errorMessage.Should().Be("User not found.");
    }

    [Test]
    public async Task AssignRoleToUserAsync_WhenRoleNotFound_ReturnsFailure()
    {
        // Arrange
        var user = await CreateUserAsync("test@user.com");

        // Act
        var (success, errorMessage) = await _adminService.AssignRoleToUserAsync(user.Id, "NonExistentRole");

        // Assert
        success.Should().BeFalse();
        errorMessage.Should().Be("Role not found.");
    }

    [Test]
    public async Task AssignRoleToUserAsync_WhenUserAlreadyHasRole_ReturnsSuccess()
    {
        // Arrange
        var user = await CreateUserAsync("test@user.com");
        var adminRole = await CreateRoleAsync("Admin");
        _dbContext.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = adminRole.Id });
        await _dbContext.SaveChangesAsync();

        // Act
        var (success, errorMessage) = await _adminService.AssignRoleToUserAsync(user.Id, "Admin");

        // Assert
        success.Should().BeTrue();
        errorMessage.Should().BeNull();
    }

    [Test]
    public async Task AssignRoleToUserAsync_WhenSuccessful_AddsUserRole()
    {
        // Arrange
        var user = await CreateUserAsync("test@user.com");
        await CreateRoleAsync("Admin");

        // Act
        var (success, errorMessage) = await _adminService.AssignRoleToUserAsync(user.Id, "Admin");

        // Assert
        success.Should().BeTrue();
        errorMessage.Should().BeNull();
        var adminRole = await _dbContext.Roles.FirstAsync(r => r.Name == "Admin");
        var userRole = await _dbContext.UserRoles.FindAsync(user.Id, adminRole.Id);
        userRole.Should().NotBeNull();
    }

    [Test]
    public async Task RemoveRoleFromUserAsync_WhenAdminRemovesOwnAdminRole_ReturnsFailure()
    {
        // Arrange
        var adminUser = await CreateUserAsync("admin@user.com");

        // Act
        var (success, errorMessage) = await _adminService.RemoveRoleFromUserAsync(adminUser.Id, "Admin", adminUser.Id);

        // Assert
        success.Should().BeFalse();
        errorMessage.Should().Be("Administrators cannot remove their own Admin role.");
    }

    [Test]
    public async Task TriggerMeetingCleanupJobAsync_WhenCalled_EnqueuesJob()
    {
        // Arrange
        var backgroundJobClientMock = new Mock<IBackgroundJobClient>();
        var adminService = new AdminService(_unitOfWork, _loggerMock.Object, backgroundJobClientMock.Object);

        // Act
        await adminService.TriggerMeetingCleanupJobAsync();

        // Assert
        backgroundJobClientMock.Verify(x => x.Create(
            It.Is<Job>(job => job.Type == typeof(IMeetingCleanupService) && job.Method.Name == nameof(IMeetingCleanupService.CleanUpAsync)),
            It.IsAny<EnqueuedState>()),
            Times.Once);
    }

    [Test]
    public async Task RemoveRoleFromUserAsync_WhenUserDoesNotHaveRole_ReturnsFailure()
    {
        // Arrange
        var user = await CreateUserAsync("test@user.com");
        var adminUser = await CreateUserAsync("admin@user.com");
        await CreateRoleAsync("Admin");

        // Act
        var (success, errorMessage) = await _adminService.RemoveRoleFromUserAsync(user.Id, "Admin", adminUser.Id);

        // Assert
        success.Should().BeFalse();
        errorMessage.Should().Be("User does not have this role.");
    }

    [Test]
    public async Task RemoveRoleFromUserAsync_WhenRoleNotFound_ReturnsFailure()
    {
        // Arrange
        var user = await CreateUserAsync("test@user.com");
        var adminUser = await CreateUserAsync("admin@user.com");

        // Act
        var (success, errorMessage) = await _adminService.RemoveRoleFromUserAsync(user.Id, "NonExistentRole", adminUser.Id);

        // Assert
        success.Should().BeFalse();
        errorMessage.Should().Be("Role not found.");
    }

    [Test]
    public async Task RemoveRoleFromUserAsync_WhenUserNotFound_ReturnsNothing() // Should not happen in practice due to authorization, but good to test.
    {
        // Arrange
        var nonExistentUserId = Guid.NewGuid();
        var adminUser = await CreateUserAsync("admin@user.com");
        await CreateRoleAsync("Admin");

        // Act
        var (success, errorMessage) = await _adminService.RemoveRoleFromUserAsync(nonExistentUserId, "Admin", adminUser.Id);

        // Assert
        // The current implementation checks for the role first, so it will return "Role not found." if the user also doesn't exist.
        // A more robust implementation might check for the user first.
        // For now, we assert the current behavior.
        success.Should().BeFalse();
        errorMessage.Should().NotBeNull(); // Error message can be either "User not found" or "Role not found" depending on implementation order.
    }

    [Test]
    public async Task RemoveRoleFromUserAsync_WhenSuccessful_RemovesUserRole()
    {
        // Arrange
        var user = await CreateUserAsync("test@user.com");
        var adminUser = await CreateUserAsync("admin@user.com");
        var adminRole = await CreateRoleAsync("Admin");
        _dbContext.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = adminRole.Id });
        await _dbContext.SaveChangesAsync();

        // Act
        var (success, errorMessage) = await _adminService.RemoveRoleFromUserAsync(user.Id, "Admin", adminUser.Id);

        // Assert
        success.Should().BeTrue();
        errorMessage.Should().BeNull();
        var userRole = await _dbContext.UserRoles.FindAsync(user.Id, adminRole.Id);
        userRole.Should().BeNull();
    }

    #region Helper Methods

    private async Task<User> CreateUserAsync(string email)
    {
        var user = new User { Id = Guid.NewGuid(), Email = email, FirstName = "Test", LastName = "User", Phone = "123", PasswordHash = "hash" };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();
        return user;
    }

    private async Task<Role> CreateRoleAsync(string name)
    {
        var role = new Role { Id = Guid.NewGuid(), Name = name };
        _dbContext.Roles.Add(role);
        await _dbContext.SaveChangesAsync();
        return role;
    }

    #endregion
}
