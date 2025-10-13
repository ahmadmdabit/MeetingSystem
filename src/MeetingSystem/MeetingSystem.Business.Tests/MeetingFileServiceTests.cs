using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using FluentAssertions;

using MeetingSystem.Business;
using MeetingSystem.Business.Configuration;
using MeetingSystem.Context;
using MeetingSystem.Model;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;

using NUnit.Framework;

namespace MeetingSystem.Business.Tests;

[TestFixture]
public class MeetingFileServiceTests 
{
    private IUnitOfWork _unitOfWork;
    private MeetingSystemDbContext _dbContext;
    private Mock<IGenericFileService> _genericFileServiceMock;
    private Mock<IOptions<MinioSettings>> _minioSettingsMock;
    private MeetingFileService _meetingFileService;

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

        _meetingFileService = new MeetingFileService(_unitOfWork, _genericFileServiceMock.Object, _minioSettingsMock.Object);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _unitOfWork.DisposeAsync();
        await _dbContext.DisposeAsync();
    }

    /// <summary>
    /// Verifies that UploadAsync fails if the uploader is not a participant of the meeting.
    /// </summary>
    [Test]
    public async Task UploadAsync_WhenUserIsNotParticipant_ReturnsError()
    {
        // Arrange
        // Create a valid user first, then use their ID as the OrganizerId.
        var organizer = new User { Id = Guid.NewGuid(), Email = "org@test.com", FirstName = "Org", LastName = "User", Phone = "123", PasswordHash = "hash" };
        var nonParticipant = new User { Id = Guid.NewGuid(), Email = "nonp@test.com", FirstName = "Non", LastName = "Participant", Phone = "456", PasswordHash = "hash" };
        _dbContext.Users.AddRange(organizer, nonParticipant);

        var meeting = new Meeting { Id = Guid.NewGuid(), Name = "Test Meeting", Description = "Test Meeting Description", OrganizerId = organizer.Id };
        _dbContext.Meetings.Add(meeting);

        await _dbContext.SaveChangesAsync();
        var files = new FormFileCollection();

        // Act
        var (resultFiles, errorMessage) = await _meetingFileService.UploadAsync(meeting.Id, files, nonParticipant.Id, CancellationToken.None);

        // Assert
        resultFiles.Should().BeNull();
        errorMessage.Should().Be("User is not a participant of this meeting.");
    }

    /// <summary>
    /// Verifies that RemoveAsync fails if the user is neither the organizer nor the uploader.
    /// </summary>
    [Test]
    public async Task RemoveAsync_WhenUserIsNotAuthorized_ReturnsError()
    {
        // Arrange
        // Create all users and link them correctly to the meeting and file.
        var organizer = new User { Id = Guid.NewGuid(), Email = "org@test.com", FirstName = "Org", LastName = "User", Phone = "123", PasswordHash = "hash" };
        var uploader = new User { Id = Guid.NewGuid(), Email = "uploader@test.com", FirstName = "Uploader", LastName = "User", Phone = "123", PasswordHash = "hash" };
        var unauthorizedUser = new User { Id = Guid.NewGuid(), Email = "other@test.com", FirstName = "Other", LastName = "User", Phone = "123", PasswordHash = "hash" };
        _dbContext.Users.AddRange(organizer, uploader, unauthorizedUser);

        var meeting = new Meeting { Id = Guid.NewGuid(), Name = "Test Meeting", Description = "Test Meeting Description", OrganizerId = organizer.Id };
        _dbContext.Meetings.Add(meeting);

        var file = new MeetingFile { Id = Guid.NewGuid(), MeetingId = meeting.Id, UploadedByUserId = uploader.Id, FileName = "test.txt", ContentType = "text/plain", MinioObjectKey = "key" };
        _dbContext.MeetingFiles.Add(file);

        await _dbContext.SaveChangesAsync();

        // Act
        var (success, errorMessage) = await _meetingFileService.RemoveAsync(meeting.Id, file.Id, unauthorizedUser.Id, CancellationToken.None);

        // Assert
        success.Should().BeFalse();
        errorMessage.Should().Be("User is not authorized to delete this file.");
    }

    /// <summary>
    /// Verifies that RemoveAsync succeeds if the user is the original uploader but not the organizer.
    /// </summary>
    [Test]
    public async Task RemoveAsync_WhenUserIsUploader_Succeeds()
    {
        // Arrange
        // Create all users and link them correctly.
        var organizer = new User { Id = Guid.NewGuid(), Email = "org@test.com", FirstName = "Org", LastName = "User", Phone = "123", PasswordHash = "hash" };
        var uploader = new User { Id = Guid.NewGuid(), Email = "uploader@test.com", FirstName = "Uploader", LastName = "User", Phone = "123", PasswordHash = "hash" };
        _dbContext.Users.AddRange(organizer, uploader);

        var meeting = new Meeting { Id = Guid.NewGuid(), Name = "Test Meeting", Description = "Test Meeting Description", OrganizerId = organizer.Id };
        _dbContext.Meetings.Add(meeting);

        var file = new MeetingFile { Id = Guid.NewGuid(), MeetingId = meeting.Id, UploadedByUserId = uploader.Id, FileName = "test.txt", ContentType = "text/plain", MinioObjectKey = "key" };
        _dbContext.MeetingFiles.Add(file);

        await _dbContext.SaveChangesAsync();

        // Act
        var (success, errorMessage) = await _meetingFileService.RemoveAsync(meeting.Id, file.Id, uploader.Id, CancellationToken.None);

        // Assert
        success.Should().BeTrue();
        errorMessage.Should().BeNull();
        _genericFileServiceMock.Verify(s => s.RemoveObjectAsync("meeting-files", file.MinioObjectKey, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verifies the successful upload of multiple files by an authorized participant.
    /// </summary>
    [Test]
    public async Task UploadAsync_WhenUserIsParticipant_UploadsFilesAndSavesToDb()
    {
        // Arrange
        var user = new User { Id = Guid.NewGuid(), Email = "participant@test.com", FirstName = "Part", LastName = "icipant", Phone = "123", PasswordHash = "hash" };
        var meeting = new Meeting { Id = Guid.NewGuid(), Name = "Test Meeting", Description = "Test Meeting Description", OrganizerId = user.Id };
        var participant = new MeetingParticipant { MeetingId = meeting.Id, UserId = user.Id };
        _dbContext.Users.Add(user);
        _dbContext.Meetings.Add(meeting);
        _dbContext.MeetingParticipants.Add(participant);
        await _dbContext.SaveChangesAsync();

        // Create realistic mock files with all required properties.
        var file1Mock = new Mock<IFormFile>();
        file1Mock.Setup(f => f.FileName).Returns("file1.txt");
        file1Mock.Setup(f => f.ContentType).Returns("text/plain");
        file1Mock.Setup(f => f.Length).Returns(1024);
        file1Mock.Setup(f => f.OpenReadStream()).Returns(new MemoryStream());

        var file2Mock = new Mock<IFormFile>();
        file2Mock.Setup(f => f.FileName).Returns("file2.log");
        file2Mock.Setup(f => f.ContentType).Returns("text/plain");
        file2Mock.Setup(f => f.Length).Returns(2048);
        file2Mock.Setup(f => f.OpenReadStream()).Returns(new MemoryStream());

        var files = new FormFileCollection { file1Mock.Object, file2Mock.Object };

        // Act
        var (resultFiles, errorMessage) = await _meetingFileService.UploadAsync(meeting.Id, files, user.Id, CancellationToken.None);

        // Assert
        errorMessage.Should().BeNull();
        resultFiles.Should().NotBeNull();
        resultFiles.Should().HaveCount(2);

        _genericFileServiceMock.Verify(s => s.UploadObjectAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IFormFile>(), true, It.IsAny<CancellationToken>()), Times.Exactly(2));

        (await _dbContext.MeetingFiles.CountAsync(f => f.MeetingId == meeting.Id)).Should().Be(2);
    }

    /// <summary>
    /// Verifies that RemoveAsync returns a "File not found" error if the file ID is invalid.
    /// </summary>
    [Test]
    public async Task RemoveAsync_WhenFileIsNotFound_ReturnsNotFoundEror()
    {
        // Arrange
        var user = new User { Id = Guid.NewGuid(), Email = "org@test.com", FirstName = "Org", LastName = "User", Phone = "123", PasswordHash = "hash" };
        var meeting = new Meeting { Id = Guid.NewGuid(), Name = "Test Meeting", Description = "Test Meeting Description", OrganizerId = user.Id };
        _dbContext.Users.Add(user);
        _dbContext.Meetings.Add(meeting);
        await _dbContext.SaveChangesAsync();
        var nonExistentFileId = Guid.NewGuid();

        // Act
        var (success, errorMessage) = await _meetingFileService.RemoveAsync(meeting.Id, nonExistentFileId, user.Id, CancellationToken.None);

        // Assert
        success.Should().BeFalse();
        errorMessage.Should().Be("File not found.");
    }

    /// <summary>
    /// Verifies that the meeting organizer can successfully remove a file they did not upload.
    /// </summary>
    [Test]
    public async Task RemoveAsync_WhenUserIsOrganizer_Succeeds()
    {
        // Arrange
        var organizer = new User { Id = Guid.NewGuid(), Email = "org@test.com", FirstName = "Org", LastName = "User", Phone = "123", PasswordHash = "hash" };
        var uploader = new User { Id = Guid.NewGuid(), Email = "uploader@test.com", FirstName = "Uploader", LastName = "User", Phone = "123", PasswordHash = "hash" };
        var meeting = new Meeting { Id = Guid.NewGuid(), Name = "Test Meeting", Description = "Test Meeting Description", OrganizerId = organizer.Id };
        var file = new MeetingFile { Id = Guid.NewGuid(), MeetingId = meeting.Id, UploadedByUserId = uploader.Id, FileName = "test.txt", ContentType = "text/plain", MinioObjectKey = "key" };
        _dbContext.Users.AddRange(organizer, uploader);
        _dbContext.Meetings.Add(meeting);
        _dbContext.MeetingFiles.Add(file);
        await _dbContext.SaveChangesAsync();

        // Act: The ORGANIZER is deleting the file.
        var (success, errorMessage) = await _meetingFileService.RemoveAsync(meeting.Id, file.Id, organizer.Id, CancellationToken.None);

        // Assert
        success.Should().BeTrue();
        errorMessage.Should().BeNull();
        _genericFileServiceMock.Verify(s => s.RemoveObjectAsync("meeting-files", file.MinioObjectKey, It.IsAny<CancellationToken>()), Times.Once);
    }
}