using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
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

namespace MeetingSystem.Business.Tests
{
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

            _meetingFileService = new MeetingFileService(_unitOfWork, _genericFileServiceMock.Object, _minioSettingsMock.Object, new Mock<ILogger<MeetingFileService>>().Object);
        }

        [TearDown]
        public async Task TearDown()
        {
            await _unitOfWork.DisposeAsync();
            await _dbContext.DisposeAsync();
        }

        [Test]
        public async Task UploadAsync_WhenUserIsNotParticipant_ReturnsError()
        {
            var organizer = await CreateUserAsync("org@test.com");
            var nonParticipant = await CreateUserAsync("nonp@test.com");
            var meeting = await CreateMeetingAsync(organizer.Id);
            var files = new FormFileCollection();
            var (resultFiles, errorMessage) = await _meetingFileService.UploadAsync(meeting.Id, files, nonParticipant.Id, true, CancellationToken.None);
            resultFiles.Should().BeNull();
            errorMessage.Should().Be("User is not a participant of this meeting.");
        }

        [Test]
        public async Task UploadAsync_WhenMeetingNotFound_ReturnsError()
        {
            var nonExistentMeetingId = Guid.NewGuid();
            var user = await CreateUserAsync("test@user.com");
            var files = new FormFileCollection();
            var (resultFiles, errorMessage) = await _meetingFileService.UploadAsync(nonExistentMeetingId, files, user.Id, true, CancellationToken.None);
            resultFiles.Should().BeNull();
            errorMessage.Should().Be("Meeting not found.");
        }

        [Test]
        public async Task RemoveAsync_WhenUserIsNotAuthorized_ReturnsError()
        {
            var organizer = await CreateUserAsync("org@test.com");
            var uploader = await CreateUserAsync("uploader@test.com");
            var unauthorizedUser = await CreateUserAsync("other@test.com");
            var meeting = await CreateMeetingAsync(organizer.Id);
            var file = new MeetingFile { Id = Guid.NewGuid(), MeetingId = meeting.Id, UploadedByUserId = uploader.Id, FileName = "test.txt", ContentType = "text/plain", MinioObjectKey = "key" };
            _dbContext.MeetingFiles.Add(file);
            await _dbContext.SaveChangesAsync();
            var (success, errorMessage) = await _meetingFileService.RemoveAsync(meeting.Id, file.Id, unauthorizedUser.Id, true, CancellationToken.None);
            success.Should().BeFalse();
            errorMessage.Should().Be("User is not authorized to delete this file.");
        }

        [Test]
        public async Task RemoveAsync_WhenUserIsUploader_Succeeds()
        {
            var organizer = await CreateUserAsync("org@test.com");
            var uploader = await CreateUserAsync("uploader@test.com");
            var meeting = await CreateMeetingAsync(organizer.Id);
            var file = new MeetingFile { Id = Guid.NewGuid(), MeetingId = meeting.Id, UploadedByUserId = uploader.Id, FileName = "test.txt", ContentType = "text/plain", MinioObjectKey = "key" };
            _dbContext.MeetingFiles.Add(file);
            await _dbContext.SaveChangesAsync();
            var (success, errorMessage) = await _meetingFileService.RemoveAsync(meeting.Id, file.Id, uploader.Id, true, CancellationToken.None);
            success.Should().BeTrue();
            errorMessage.Should().BeNull();
            _genericFileServiceMock.Verify(s => s.RemoveObjectAsync("meeting-files", file.MinioObjectKey, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task UploadAsync_WhenUserIsParticipant_UploadsFilesAndSavesToDb()
        {
            var user = await CreateUserAsync("participant@test.com");
            var meeting = await CreateMeetingAsync(user.Id);
            var participant = new MeetingParticipant { MeetingId = meeting.Id, UserId = user.Id };
            _dbContext.MeetingParticipants.Add(participant);
            await _dbContext.SaveChangesAsync();
            var file1Mock = new Mock<IFormFile>();
            file1Mock.Setup(f => f.FileName).Returns("file1.txt");
            file1Mock.Setup(f => f.ContentType).Returns("text/plain");
            var files = new FormFileCollection { file1Mock.Object };
            var (resultFiles, errorMessage) = await _meetingFileService.UploadAsync(meeting.Id, files, user.Id, true, CancellationToken.None);
            errorMessage.Should().BeNull();
            resultFiles.Should().NotBeNull();
            resultFiles.Should().HaveCount(1);
            _genericFileServiceMock.Verify(s => s.UploadObjectAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IFormFile>(), true, It.IsAny<CancellationToken>()), Times.Once);
            (await _dbContext.MeetingFiles.CountAsync(f => f.MeetingId == meeting.Id)).Should().Be(1);
        }

        [Test]
        public async Task RemoveAsync_WhenFileIsNotFound_ReturnsNotFoundEror()
        {
            var user = await CreateUserAsync("org@test.com");
            var meeting = await CreateMeetingAsync(user.Id);
            var nonExistentFileId = Guid.NewGuid();
            var (success, errorMessage) = await _meetingFileService.RemoveAsync(meeting.Id, nonExistentFileId, user.Id, true, CancellationToken.None);
            success.Should().BeFalse();
            errorMessage.Should().Be("File not found.");
        }

        [Test]
        public async Task RemoveAsync_WhenUserIsOrganizer_Succeeds()
        {
            var organizer = await CreateUserAsync("org@test.com");
            var uploader = await CreateUserAsync("uploader@test.com");
            var meeting = await CreateMeetingAsync(organizer.Id);
            var file = new MeetingFile { Id = Guid.NewGuid(), MeetingId = meeting.Id, UploadedByUserId = uploader.Id, FileName = "test.txt", ContentType = "text/plain", MinioObjectKey = "key" };
            _dbContext.MeetingFiles.Add(file);
            await _dbContext.SaveChangesAsync();
            var (success, errorMessage) = await _meetingFileService.RemoveAsync(meeting.Id, file.Id, organizer.Id, true, CancellationToken.None);
            success.Should().BeTrue();
            errorMessage.Should().BeNull();
            _genericFileServiceMock.Verify(s => s.RemoveObjectAsync("meeting-files", file.MinioObjectKey, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Test]
        public async Task UploadAsync_WhenStorageFails_ThrowsAndRollsBack()
        {
            var user = await CreateUserAsync("participant@test.com");
            var meeting = await CreateMeetingAsync(user.Id);
            var participant = new MeetingParticipant { MeetingId = meeting.Id, UserId = user.Id };
            _dbContext.MeetingParticipants.Add(participant);
            await _dbContext.SaveChangesAsync();
            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(f => f.FileName).Returns("file1.txt");
            var files = new FormFileCollection { fileMock.Object };
            _genericFileServiceMock.Setup(s => s.UploadObjectAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IFormFile>(), true, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Simulated storage failure"));
            Func<Task> act = async () => await _meetingFileService.UploadAsync(meeting.Id, files, user.Id, true, CancellationToken.None);
            await act.Should().ThrowAsync<Exception>().WithMessage("Simulated storage failure");
            (await _dbContext.MeetingFiles.CountAsync()).Should().Be(0);
        }

        [Test]
        public async Task UploadAsync_WhenCommitFails_ThrowsAndRollsBack()
        {
            var user = await CreateUserAsync("participant@test.com");
            var meeting = await CreateMeetingAsync(user.Id);
            var participant = new MeetingParticipant { MeetingId = meeting.Id, UserId = user.Id };
            _dbContext.MeetingParticipants.Add(participant);
            await _dbContext.SaveChangesAsync();

            var file1Mock = new Mock<IFormFile>();
            file1Mock.Setup(f => f.FileName).Returns("file1.txt");
            file1Mock.Setup(f => f.ContentType).Returns("text/plain");
            var files = new FormFileCollection { file1Mock.Object };

            var mockUnitOfWork = new Mock<IUnitOfWork>();
            mockUnitOfWork.Setup(u => u.Meetings).Returns(_unitOfWork.Meetings);
            mockUnitOfWork.Setup(u => u.MeetingParticipants).Returns(_unitOfWork.MeetingParticipants);
            mockUnitOfWork.Setup(u => u.MeetingFiles).Returns(_unitOfWork.MeetingFiles);
            mockUnitOfWork.Setup(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Simulated commit failure"));

            var meetingFileService = new MeetingFileService(
                mockUnitOfWork.Object,
                _genericFileServiceMock.Object,
                _minioSettingsMock.Object,
                new Mock<ILogger<MeetingFileService>>().Object);

            Func<Task> act = () => meetingFileService.UploadAsync(meeting.Id, files, user.Id, true, CancellationToken.None);
            await act.Should().ThrowAsync<Exception>().WithMessage("Simulated commit failure");
        }

        [Test]
        public async Task RemoveAsync_WhenStorageFails_ThrowsAndRollsBack()
        {
            var organizer = await CreateUserAsync("org@test.com");
            var meeting = await CreateMeetingAsync(organizer.Id);
            var file = new MeetingFile { Id = Guid.NewGuid(), MeetingId = meeting.Id, UploadedByUserId = organizer.Id, FileName = "test.txt", ContentType = "text/plain", MinioObjectKey = "key" };
            _dbContext.MeetingFiles.Add(file);
            await _dbContext.SaveChangesAsync();
            _genericFileServiceMock.Setup(s => s.RemoveObjectAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Simulated storage failure"));
            Func<Task> act = async () => await _meetingFileService.RemoveAsync(meeting.Id, file.Id, organizer.Id, true, CancellationToken.None);
            await act.Should().ThrowAsync<Exception>().WithMessage("Simulated storage failure");
            _dbContext.Entry(file).State.Should().NotBe(EntityState.Detached);
        }

        #region GetMeetingFilesAsync Tests

        [Test]
        public async Task GetMeetingFilesAsync_WhenUserIsNotParticipant_ReturnsError()
        {
            var organizer = await CreateUserAsync("org@test.com");
            var nonParticipant = await CreateUserAsync("non-participant@test.com");
            var meeting = await CreateMeetingAsync(organizer.Id);
            var (files, errorMessage) = await _meetingFileService.GetMeetingFilesAsync(meeting.Id, nonParticipant.Id);
            files.Should().BeNull();
            errorMessage.Should().Be("User is not a participant of this meeting.");
        }

        [Test]
        public async Task GetMeetingFilesAsync_WhenUserIsParticipant_ReturnsFiles()
        {
            var organizer = await CreateUserAsync("org@test.com");
            var meeting = await CreateMeetingAsync(organizer.Id);
            _dbContext.MeetingParticipants.Add(new MeetingParticipant { MeetingId = meeting.Id, UserId = organizer.Id });
            _dbContext.MeetingFiles.Add(new MeetingFile { Id = Guid.NewGuid(), MeetingId = meeting.Id, FileName = "test.txt", UploadedByUserId = organizer.Id, ContentType = "text/plain", MinioObjectKey = "key" });
            await _dbContext.SaveChangesAsync();
            var (files, errorMessage) = await _meetingFileService.GetMeetingFilesAsync(meeting.Id, organizer.Id);
            errorMessage.Should().BeNull();
            files.Should().NotBeNull();
            files.Should().HaveCount(1);
            files!.First().FileName.Should().Be("test.txt");
        }

        #endregion

        #region GetFileDownloadUrlAsync Tests

        [Test]
        public async Task GetFileDownloadUrlAsync_WhenUserIsNotParticipant_ReturnsError()
        {
            var organizer = await CreateUserAsync("org@test.com");
            var nonParticipant = await CreateUserAsync("non-participant@test.com");
            var meeting = await CreateMeetingAsync(organizer.Id);
            var file = new MeetingFile { Id = Guid.NewGuid(), MeetingId = meeting.Id, FileName = "test.txt", UploadedByUserId = organizer.Id, ContentType = "text/plain", MinioObjectKey = "key" };
            _dbContext.MeetingFiles.Add(file);
            await _dbContext.SaveChangesAsync();
            var (url, errorMessage) = await _meetingFileService.GetFileDownloadUrlAsync(meeting.Id, file.Id, nonParticipant.Id);
            url.Should().BeNull();
            errorMessage.Should().Be("User is not a participant of this meeting.");
        }

        [Test]
        public async Task GetFileDownloadUrlAsync_WhenFileNotFound_ReturnsError()
        {
            var organizer = await CreateUserAsync("org@test.com");
            var meeting = await CreateMeetingAsync(organizer.Id);
            _dbContext.MeetingParticipants.Add(new MeetingParticipant { MeetingId = meeting.Id, UserId = organizer.Id });
            await _dbContext.SaveChangesAsync();
            var nonExistentFileId = Guid.NewGuid();
            var (url, errorMessage) = await _meetingFileService.GetFileDownloadUrlAsync(meeting.Id, nonExistentFileId, organizer.Id);
            url.Should().BeNull();
            errorMessage.Should().Be("File not found.");
        }

        [Test]
        public async Task GetFileDownloadUrlAsync_WhenSuccessful_ReturnsUrl()
        {
            var organizer = await CreateUserAsync("org@test.com");
            var meeting = await CreateMeetingAsync(organizer.Id);
            var file = new MeetingFile { Id = Guid.NewGuid(), MeetingId = meeting.Id, FileName = "test.txt", UploadedByUserId = organizer.Id, ContentType = "text/plain", MinioObjectKey = "the-object-key" };
            _dbContext.MeetingParticipants.Add(new MeetingParticipant { MeetingId = meeting.Id, UserId = organizer.Id });
            _dbContext.MeetingFiles.Add(file);
            await _dbContext.SaveChangesAsync();
            _genericFileServiceMock.Setup(s => s.GetPresignedUrlAsync("meeting-files", "the-object-key", It.IsAny<CancellationToken>()))
                .ReturnsAsync("http://presigned-url.com/the-object-key");
            var (url, errorMessage) = await _meetingFileService.GetFileDownloadUrlAsync(meeting.Id, file.Id, organizer.Id);
            errorMessage.Should().BeNull();
            url.Should().Be("http://presigned-url.com/the-object-key");
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

        private async Task<Meeting> CreateMeetingAsync(Guid organizerId)
        {
            var meeting = new Meeting { Id = Guid.NewGuid(), Name = "Test Meeting", Description = "Test Meeting Description", OrganizerId = organizerId };
            _dbContext.Meetings.Add(meeting);
            await _dbContext.SaveChangesAsync();
            return meeting;
        }

        #endregion
    }
}
