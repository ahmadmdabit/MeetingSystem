using FluentAssertions;

using MeetingSystem.Business.Configuration;
using MeetingSystem.Context;
using MeetingSystem.Model;

using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;

namespace MeetingSystem.Business.Tests;

[TestFixture]
public class ProfilePictureServiceTests
{
    private IUnitOfWork _unitOfWork;
    private MeetingSystemDbContext _dbContext;
    private Mock<IGenericFileService> _genericFileServiceMock;
    private Mock<IOptions<MinioSettings>> _minioSettingsMock;
    private ProfilePictureService _profilePictureService;

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

        _genericFileServiceMock = new Mock<IGenericFileService>();
        _minioSettingsMock = new Mock<IOptions<MinioSettings>>();
        _minioSettingsMock.Setup(s => s.Value).Returns(new MinioSettings { PublicEndpoint = "http://localhost:9000", Buckets = new Buckets { Profile = "profile-pics", Meeting = "meeting-files" } });

        _profilePictureService = new ProfilePictureService(_unitOfWork, _genericFileServiceMock.Object, _minioSettingsMock.Object, new Mock<ILogger<ProfilePictureService>>().Object);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _unitOfWork.DisposeAsync();
        await _dbContext.DisposeAsync();
    }

    /// <summary>
    /// Verifies that SetAsync returns a failure status if the user does not exist.
    /// </summary>
    [Test]
    public async Task SetAsync_WhenUserNotFound_ReturnsFailure()
    {
        // Arrange
        var nonExistentUserId = Guid.NewGuid();
        var fileMock = new Mock<IFormFile>();

        // Act
        var (status, errorMessage) = await _profilePictureService.SetAsync(nonExistentUserId, fileMock.Object, true, CancellationToken.None);

        // Assert
        status.Should().BeFalse();
        errorMessage.Should().Be("User not found.");
    }

    /// <summary>
    /// Verifies that SetAsync deletes the old picture if one already exists.
    /// </summary>
    [Test]
    public async Task SetAsync_WhenUserHasOldPicture_RemovesOldPicture()
    {
        // Arrange
        var user = new User { Id = Guid.NewGuid(), ProfilePictureUrl = "old-key", Email = "test@test.com", FirstName = "Test", LastName = "User", Phone = "123", PasswordHash = "hash" };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();
        var fileMock = new Mock<IFormFile>();

        // Act
        var (status, _) = await _profilePictureService.SetAsync(user.Id, fileMock.Object, true, CancellationToken.None);

        // Assert
        status.Should().BeTrue();
        _genericFileServiceMock.Verify(s => s.RemoveObjectAsync("profile-pics", "old-key", It.IsAny<CancellationToken>()), Times.Once);
        _genericFileServiceMock.Verify(s => s.UploadObjectAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IFormFile>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verifies that RemoveAsync returns false if the user has no profile picture to remove.
    /// </summary>
    [Test]
    public async Task RemoveAsync_WhenUserHasNoPicture_ReturnsFalse()
    {
        // Arrange
        var user = new User { Id = Guid.NewGuid(), ProfilePictureUrl = null, Email = "test@test.com", FirstName = "Test", LastName = "User", Phone = "123", PasswordHash = "hash" };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _profilePictureService.RemoveAsync(user.Id, true, CancellationToken.None);

        // Assert
        result.Status.Should().BeFalse();
        result.ErrorMessage.Should().Be("User does not have a profile picture.");
        _genericFileServiceMock.Verify(s => s.RemoveObjectAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Verifies that RemoveAsync successfully removes a picture and updates the user record.
    /// </summary>
    [Test]
    public async Task RemoveAsync_WhenUserHasPicture_SucceedsAndReturnsTrue()
    {
        // Arrange
        var user = new User { Id = Guid.NewGuid(), ProfilePictureUrl = "existing-key", Email = "test@test.com", FirstName = "Test", LastName = "User", Phone = "123", PasswordHash = "hash" };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _profilePictureService.RemoveAsync(user.Id, true, CancellationToken.None);

        // Assert
        result.Status.Should().BeTrue();
        _genericFileServiceMock.Verify(s => s.RemoveObjectAsync("profile-pics", "existing-key", It.IsAny<CancellationToken>()), Times.Once);

        var updatedUser = await _dbContext.Users.FindAsync(user.Id);
        updatedUser!.ProfilePictureUrl.Should().BeNull();
    }

    /// <summary>
    /// Verifies that GetUrlAsync returns a valid URL for a user with a profile picture.
    /// </summary>
    [Test]
    public async Task GetUrlAsync_WhenUserHasPicture_ReturnsPresignedUrl()
    {
        // Arrange
        var user = new User { Id = Guid.NewGuid(), ProfilePictureUrl = "picture-key", Email = "test@test.com", FirstName = "Test", LastName = "User", Phone = "123", PasswordHash = "hash" };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var expectedUrl = "http://presigned-url";
        _genericFileServiceMock.Setup(s => s.GetPresignedUrlAsync("profile-pics", "picture-key", 86400, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedUrl);

        // Act
        var resultUrl = await _profilePictureService.GetUrlAsync(user.Id, CancellationToken.None);

        // Assert
        resultUrl.Should().Be(expectedUrl);
    }

    /// <summary>
    /// Verifies that GetUrlAsync returns null for a user without a profile picture.
    /// </summary>
    [Test]
    public async Task GetUrlAsync_WhenUserHasNoPicture_ReturnsNull()
    {
        // Arrange
        var user = new User { Id = Guid.NewGuid(), ProfilePictureUrl = null, Email = "test@test.com", FirstName = "Test", LastName = "User", Phone = "123", PasswordHash = "hash" };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Act
        var resultUrl = await _profilePictureService.GetUrlAsync(user.Id, CancellationToken.None);

        // Assert
        resultUrl.Should().BeNull();
    }

    // ***** NEW TEST CASE TO COVER MISSING BRANCH *****
    /// <summary>
    /// Verifies that RemoveAsync returns a failure status if the user does not exist.
    /// </summary>
    [Test]
    public async Task RemoveAsync_WhenUserNotFound_ReturnsFailure()
    {
        // Arrange
        var nonExistentUserId = Guid.NewGuid();

        // Act
        var (status, errorMessage) = await _profilePictureService.RemoveAsync(nonExistentUserId, true, CancellationToken.None);

        // Assert
        status.Should().BeFalse();
        errorMessage.Should().Be("User not found.");
    }

    // ***** NEW TEST CASE TO COVER MISSING BRANCH *****
    /// <summary>
    /// Verifies that GetUrlAsync returns null when the user ID does not exist.
    /// </summary>
    [Test]
    public async Task GetUrlAsync_WhenUserNotFound_ReturnsNull()
    {
        // Arrange
        var nonExistentUserId = Guid.NewGuid();

        // Act
        var result = await _profilePictureService.GetUrlAsync(nonExistentUserId, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    /// <summary>
    /// Verifies that the catch block in SetAsync is triggered when the file service throws an exception.
    /// </summary>
    [Test]
    public async Task SetAsync_WhenStorageFails_ThrowsAndRollsBack()
    {
        // Arrange
        var user = new User { Id = Guid.NewGuid(), Email = "test@test.com", FirstName = "Test", LastName = "User", Phone = "123", PasswordHash = "hash" };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();
        var fileMock = new Mock<IFormFile>();

        _genericFileServiceMock.Setup(s => s.UploadObjectAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IFormFile>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Simulated storage failure"));

        // Act
        Func<Task> act = async () => await _profilePictureService.SetAsync(user.Id, fileMock.Object, true, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<Exception>().WithMessage("Simulated storage failure");
    }
}