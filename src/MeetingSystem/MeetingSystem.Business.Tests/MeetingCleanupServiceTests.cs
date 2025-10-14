using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MeetingSystem.Business.Configuration;
using MeetingSystem.Context;
using MeetingSystem.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MockQueryable.Moq;
using Moq;

namespace MeetingSystem.Business.Tests;

[TestFixture]
public class MeetingCleanupServiceTests
{
    private Mock<IUnitOfWork> _unitOfWorkMock;
    private Mock<IMeetingFileService> _meetingFileServiceMock;
    private Mock<IOptions<HangfireSettings>> _hangfireSettingsOptionsMock;
    private Mock<IOptions<MinioSettings>> _minioSettingsOptionsMock;
    private Mock<ILogger<MeetingCleanupService>> _loggerMock;
    private MeetingCleanupService _cleanupService;

    [SetUp]
    public void SetUp()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _meetingFileServiceMock = new Mock<IMeetingFileService>();
        _hangfireSettingsOptionsMock = new Mock<IOptions<HangfireSettings>>();
        _minioSettingsOptionsMock = new Mock<IOptions<MinioSettings>>();
        _loggerMock = new Mock<ILogger<MeetingCleanupService>>();

        _hangfireSettingsOptionsMock.Setup(s => s.Value).Returns(new HangfireSettings 
        {
            CleanupThresholdDays = 30,
            DashboardAdminRole = "Admin",
            CleanupJobCronExpression = "* * * * *"
        });
        _minioSettingsOptionsMock.Setup(s => s.Value).Returns(new MinioSettings 
        {
            Buckets = new Buckets 
            {
                Meeting = "meeting-files", 
                Profile = "profile-pics"
            },
            PublicEndpoint = "http://localhost:9000"
        });

        _cleanupService = new MeetingCleanupService(
            _unitOfWorkMock.Object,
            _meetingFileServiceMock.Object,
            _minioSettingsOptionsMock.Object,
            _hangfireSettingsOptionsMock.Object,
            _loggerMock.Object);
    }

    [Test]
    public async Task CleanUpAsync_WhenNoMeetingsAreOldEnough_DoesNothing()
    {
        // Arrange
        var emptyMeetingList = new List<Meeting>();
        var mockDbSet = emptyMeetingList.BuildMockDbSet();
        _unitOfWorkMock.Setup(u => u.Meetings.Find(It.IsAny<Expression<Func<Meeting, bool>>>()))
                       .Returns(mockDbSet.Object);

        // Act
        await _cleanupService.CleanUpAsync(true, CancellationToken.None);

        // Assert
        _loggerMock.Verify(
            logger => logger.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("No meetings found for cleanup")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.Meetings.RemoveRange(It.IsAny<IEnumerable<Meeting>>()), Times.Never);
    }

    [Test]
    public async Task CleanUpAsync_WhenMeetingsAreFound_DeletesMeetingsAndAssociatedFiles()
    {
        // Arrange
        var meeting1Id = Guid.NewGuid();
        var meeting2Id = Guid.NewGuid();

        var organizerId = Guid.NewGuid();

        var userId = Guid.NewGuid();

        var meetingsToClean = new List<Meeting>
        {
            new Meeting {
                Id = meeting1Id,
                OrganizerId = organizerId,
                Name = "Old Meeting 1",
                Description = "Desc",
                Files = new List<MeetingFile> {
                    new MeetingFile {
                        Id = Guid.NewGuid(),
                        MeetingId = meeting1Id,
                        UploadedByUserId = organizerId,
                        FileName = "test.txt",
                        ContentType = "text/plain",
                        MinioObjectKey = "key"
                    }
                }
            },
            new Meeting {
                Id = meeting2Id,
                OrganizerId = organizerId,
                Name = "Old Meeting 2",
                Description = "Desc",
                Files = new List<MeetingFile> {
                    new MeetingFile {
                        Id = Guid.NewGuid(),
                        MeetingId = meeting2Id,
                        UploadedByUserId = organizerId,
                        FileName = "test2.txt",
                        ContentType = "text/plain",
                        MinioObjectKey = "key2"
                    },
                    new MeetingFile {
                        Id = Guid.NewGuid(),
                        MeetingId = meeting2Id,
                        UploadedByUserId = userId,
                        FileName = "test3.txt",
                        ContentType = "text/plain",
                        MinioObjectKey = "key3"
                    }
                }
            }
        };
        var mockDbSet = meetingsToClean.BuildMockDbSet();
        _unitOfWorkMock.Setup(u => u.Meetings.Find(It.IsAny<Expression<Func<Meeting, bool>>>()))
                       .Returns(mockDbSet.Object);

        _meetingFileServiceMock.Setup(mfs => mfs.RemoveAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                               .ReturnsAsync((true, null)); // Simulate success

        // Act
        await _cleanupService.CleanUpAsync(true, CancellationToken.None);

        // Assert
        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _meetingFileServiceMock.Verify(mfs => mfs.RemoveAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Exactly(3)); // 1 file in meeting1, 2 in meeting2
        _unitOfWorkMock.Verify(u => u.Meetings.RemoveRange(meetingsToClean), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task CleanUpAsync_WhenFileServiceFails_ThrowsExceptionAndRollsBackTransaction()
    {
        // Arrange
        var meetingId = Guid.NewGuid();
        var organizerId = Guid.NewGuid();
        var meetingsToClean = new List<Meeting>
        {
            new Meeting { Id = meetingId, OrganizerId = organizerId, Name = "Meeting with failing file", Description = "Desc",
                Files = new List<MeetingFile> 
                {
                    new MeetingFile {
                        Id = Guid.NewGuid(),
                        MeetingId = meetingId,
                        UploadedByUserId = organizerId,
                        FileName = "test.txt",
                        ContentType = "text/plain",
                        MinioObjectKey = "key"
                    }
                }
            }
        };
        var mockDbSet = meetingsToClean.BuildMockDbSet();
        _unitOfWorkMock.Setup(u => u.Meetings.Find(It.IsAny<Expression<Func<Meeting, bool>>>()))
                       .Returns(mockDbSet.Object);

        _meetingFileServiceMock.Setup(mfs => mfs.RemoveAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                               .ReturnsAsync((false, "MinIO unavailable")); // Simulate failure

        // Act
        Func<Task> act = async () => await _cleanupService.CleanUpAsync(true, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Failed to queue removal for file*");
        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task CleanUpAsync_WhenCommitFails_ThrowsExceptionAndRollsBackTransaction()
    {
        // Arrange
        var meetingId = Guid.NewGuid();
        var meetingsToClean = new List<Meeting>
        {
            new Meeting { Id = meetingId, OrganizerId = Guid.NewGuid(), Name = "Meeting with commit failure", Description = "Desc", Files = new List<MeetingFile>() }
        };
        var mockDbSet = meetingsToClean.BuildMockDbSet();
        _unitOfWorkMock.Setup(u => u.Meetings.Find(It.IsAny<Expression<Func<Meeting, bool>>>()))
                       .Returns(mockDbSet.Object);

        _unitOfWorkMock.Setup(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()))
                       .ThrowsAsync(new DbUpdateException("Simulated database connection lost"));

        // Act
        Func<Task> act = async () => await _cleanupService.CleanUpAsync(true, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();
        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.Meetings.RemoveRange(meetingsToClean), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once); // It was attempted
        _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once); // But then rolled back
    }

    [Test]
    public async Task CleanUpAsync_WhenCommitFails_ThrowsAndRollsBack()
    {
        // Arrange
        var meetingId = Guid.NewGuid();
        var meetingsToClean = new List<Meeting>
        {
            new Meeting { Id = meetingId, OrganizerId = Guid.NewGuid(), Name = "Meeting with commit failure", Description = "Desc", IsCanceled = true, CanceledAt = DateTime.UtcNow.AddDays(-40) }
        };
        var mockDbSet = meetingsToClean.BuildMockDbSet();
        _unitOfWorkMock.Setup(u => u.Meetings.Find(It.IsAny<Expression<Func<Meeting, bool>>>())).Returns(mockDbSet.Object);
        _unitOfWorkMock.Setup(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new Exception("Simulated commit failure"));

        // Act
        Func<Task> act = () => _cleanupService.CleanUpAsync(true, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<Exception>().WithMessage("Simulated commit failure");
        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.Meetings.RemoveRange(It.IsAny<IEnumerable<Meeting>>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}