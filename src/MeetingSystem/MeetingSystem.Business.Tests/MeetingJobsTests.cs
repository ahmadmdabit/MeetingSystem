using MeetingSystem.Business.Jobs;
using MeetingSystem.Context;
using MeetingSystem.Model;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Moq;

namespace MeetingSystem.Business.Tests;

[TestFixture]
public class MeetingJobsTests
{
    private IUnitOfWork _unitOfWork;
    private MeetingSystemDbContext _dbContext;
    private Mock<IEmailService> _emailServiceMock;
    private Mock<ILogger<MeetingJobs>> _loggerMock;
    private MeetingJobs _meetingJobs;

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

        _emailServiceMock = new Mock<IEmailService>();
        _loggerMock = new Mock<ILogger<MeetingJobs>>();

        _meetingJobs = new MeetingJobs(_unitOfWork, _emailServiceMock.Object, _loggerMock.Object);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _unitOfWork.DisposeAsync();
        await _dbContext.DisposeAsync();
    }

    /// <summary>
    /// Verifies that SendReminderAsync sends an email to all participants of a valid meeting.
    /// </summary>
    [Test]
    public async Task SendReminderAsync_WithValidMeeting_SendsEmailToAllParticipants()
    {
        // Arrange
        var user1 = new User { Id = Guid.NewGuid(), Email = "user1@test.com", FirstName = "User", LastName = "One", Phone = "123", PasswordHash = "hash" };
        var user2 = new User { Id = Guid.NewGuid(), Email = "user2@test.com", FirstName = "User", LastName = "Two", Phone = "123", PasswordHash = "hash" };
        var meeting = new Meeting { Id = Guid.NewGuid(), Name = "Reminder Test", Description = "Test Meeting Description", OrganizerId = user1.Id };
        var participant1 = new MeetingParticipant { MeetingId = meeting.Id, UserId = user1.Id };
        var participant2 = new MeetingParticipant { MeetingId = meeting.Id, UserId = user2.Id };
        _dbContext.Users.AddRange(user1, user2);
        _dbContext.Meetings.Add(meeting);
        _dbContext.MeetingParticipants.AddRange(participant1, participant2);
        await _dbContext.SaveChangesAsync();

        // Act
        await _meetingJobs.SendReminderAsync(meeting.Id, CancellationToken.None);

        // Assert
        // Verify that the email service was called for each participant.
        _emailServiceMock.Verify(s => s.SendMeetingReminderAsync(user1.Email, meeting.Name, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        _emailServiceMock.Verify(s => s.SendMeetingReminderAsync(user2.Email, meeting.Name, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verifies that SendReminderAsync does nothing and logs a warning if the meeting ID is not found.
    /// </summary>
    [Test]
    public async Task SendReminderAsync_WithInvalidMeetingId_DoesNothingAndLogsWarning()
    {
        // Arrange
        var nonExistentMeetingId = Guid.NewGuid();

        // Act
        await _meetingJobs.SendReminderAsync(nonExistentMeetingId, CancellationToken.None);

        // Assert
        // Verify that no emails were sent.
        _emailServiceMock.Verify(s => s.SendMeetingReminderAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);

        // Verify that a warning was logged.
        _loggerMock.Verify(
            logger => logger.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("not found while trying to send reminder")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}