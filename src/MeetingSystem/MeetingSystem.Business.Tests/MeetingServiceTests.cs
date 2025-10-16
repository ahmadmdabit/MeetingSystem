using FluentAssertions;

using Hangfire;
using Hangfire.Common;
using Hangfire.States;

using MeetingSystem.Business.Dtos;
using MeetingSystem.Business.Jobs;
using MeetingSystem.Context;
using MeetingSystem.Model;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

using MockQueryable.Moq;

using Moq;

namespace MeetingSystem.Business.Tests;

[TestFixture]
public class MeetingServiceTests
{
    private IUnitOfWork _unitOfWork;
    private MeetingSystemDbContext _dbContext;
    private Mock<IBackgroundJobClient> _backgroundJobClientMock;
    private Mock<ILogger<MeetingService>> _loggerMock;
    private MeetingService _meetingService;

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

        _backgroundJobClientMock = new Mock<IBackgroundJobClient>();
        _loggerMock = new Mock<ILogger<MeetingService>>();

        _meetingService = new MeetingService(_unitOfWork, _backgroundJobClientMock.Object, _loggerMock.Object);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _unitOfWork.DisposeAsync();
        await _dbContext.DisposeAsync();
    }

    /// <summary>
    /// Verifies that CreateMeetingAsync throws an exception if the organizer ID is invalid.
    /// </summary>
    [Test]
    public void CreateMeetingAsync_WithInvalidOrganizer_ThrowsInvalidOperationException()
    {
        // Arrange
        var dto = new CreateMeetingDto("Test", "Desc", DateTime.UtcNow, DateTime.UtcNow.AddHours(1), null);
        var nonExistentOrganizerId = Guid.NewGuid();

        // Act
        Func<Task> act = async () => await _meetingService.CreateMeetingAsync(dto, nonExistentOrganizerId, true, CancellationToken.None);

        // Assert
        act.Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary>
    /// Verifies that UpdateMeetingAsync fails if the user is not the meeting organizer.
    /// </summary>
    [Test]
    public async Task UpdateMeetingAsync_WhenUserIsNotOrganizer_ReturnsError()
    {
        // Arrange
        var organizer = await CreateUserAsync("org@test.com");
        var meeting = await CreateMeetingAsync(organizer.Id);
        var nonOrganizer = await CreateUserAsync("nonorg@test.com");
        var dto = new UpdateMeetingDto("New Name", "New Desc", DateTime.UtcNow, DateTime.UtcNow.AddHours(1), ["org@test.com", "nonorg@test.com"]);

        // Act
        var (updatedMeeting, errorMessage) = await _meetingService.UpdateMeetingAsync(meeting.Id, dto, nonOrganizer.Id, CancellationToken.None);

        // Assert
        updatedMeeting.Should().BeNull();
        errorMessage.Should().Be("User is not authorized to update this meeting.");
    }

    /// <summary>
    /// Verifies that UpdateMeetingAsync returns null if the meeting is not found.
    /// </summary>
    [Test]
    public async Task UpdateMeetingAsync_WhenMeetingNotFound_ReturnsNull()
    {
        // Arrange
        var user = await CreateUserAsync("user@test.com");
        var dto = new UpdateMeetingDto("New Name", "New Desc", DateTime.UtcNow, DateTime.UtcNow.AddHours(1), ["org@test.com", "nonorg@test.com"]);
        var nonExistentMeetingId = Guid.NewGuid();

        // Act
        var (updatedMeeting, errorMessage) = await _meetingService.UpdateMeetingAsync(nonExistentMeetingId, dto, user.Id, CancellationToken.None);

        // Assert
        updatedMeeting.Should().BeNull();
        errorMessage.Should().Be("Meeting not found");
    }

    /// <summary>
    /// Verifies that CancelMeetingAsync returns false if the user is not the organizer.
    /// </summary>
    [Test]
    public async Task CancelMeetingAsync_WhenUserIsNotOrganizer_ReturnsFalse()
    {
        // Arrange
        var organizer = await CreateUserAsync("org@test.com");
        var meeting = await CreateMeetingAsync(organizer.Id);
        var nonOrganizer = await CreateUserAsync("nonorg@test.com");

        // Act
        var result = await _meetingService.CancelMeetingAsync(meeting.Id, nonOrganizer.Id, CancellationToken.None);

        // Assert
        result.Status.Should().BeFalse();
    }

    /// <summary>
    /// Verifies that AddParticipantAsync fails if the user to be added does not exist.
    /// </summary>
    [Test]
    public async Task AddParticipantAsync_WhenUserToAddNotFound_ReturnsFalse()
    {
        // Arrange
        var organizer = await CreateUserAsync("org@test.com");
        var meeting = await CreateMeetingAsync(organizer.Id);

        // Act
        var result = await _meetingService.AddParticipantAsync(meeting.Id, "nonexistent@example.com", organizer.Id, CancellationToken.None);

        // Assert
        result.Status.Should().BeFalse();
    }

    /// <summary>
    /// Verifies that RemoveParticipantAsync fails if the participant to be removed is not in the meeting.
    /// </summary>
    [Test]
    public async Task RemoveParticipantAsync_WhenParticipantIsNotInMeeting_ReturnsFalse()
    {
        // Arrange
        var organizer = await CreateUserAsync("org@test.com");
        var meeting = await CreateMeetingAsync(organizer.Id);
        var userToRemove = await CreateUserAsync("toremove@test.com");

        // Act
        var result = await _meetingService.RemoveParticipantAsync(meeting.Id, userToRemove.Id, organizer.Id, CancellationToken.None);

        // Assert
        result.Status.Should().BeFalse();
    }

    /// <summary>
    /// Verifies the successful creation of a meeting, including participant assignment and job scheduling.
    /// </summary>
    [Test]
    public async Task CreateMeetingAsync_WithValidData_CreatesMeetingAndSchedulesJob()
    {
        // Arrange
        var organizer = await CreateUserAsync("organizer@test.com");
        var participantUser = await CreateUserAsync("participant@test.com");
        var dto = new CreateMeetingDto(
            "New Meeting",
            "Test Description",
            DateTime.UtcNow.AddHours(1),
            DateTime.UtcNow.AddHours(2),
            new List<string> { participantUser.Email }
        );

        // Act
        var resultDto = await _meetingService.CreateMeetingAsync(dto, organizer.Id, true, CancellationToken.None);

        // Assert
        resultDto.Should().NotBeNull();
        resultDto!.Name.Should().Be(dto.Name);
        resultDto.Participants.Should().HaveCount(2); // Organizer + 1 participant

        // Verify database state
        var meetingInDb = await _dbContext.Meetings.FindAsync(resultDto.Id);
        meetingInDb.Should().NotBeNull();
        var participantsInDb = await _dbContext.MeetingParticipants.Where(p => p.MeetingId == resultDto.Id).ToListAsync();
        participantsInDb.Should().HaveCount(2);

        // Verify the underlying 'Create' method for a scheduled job
        _backgroundJobClientMock.Verify(
            client => client.Create(
                It.Is<Job>(job =>
                    job.Method.DeclaringType == typeof(IMeetingJobs) &&
                    job.Method.Name == nameof(IMeetingJobs.SendReminderAsync)
                ),
                It.IsAny<ScheduledState>() // Check that it's being added to the "Scheduled" state.
            ),
            Times.Once
        );
    }

    /// <summary>
    /// Verifies that GetUserMeetingsAsync returns a list of meetings the user is a part of.
    /// </summary>
    [Test]
    public async Task GetUserMeetingsAsync_WhenUserHasMeetings_ReturnsMeetingList()
    {
        // Arrange
        var user = await CreateUserAsync("user@test.com");
        var meeting1 = await CreateMeetingAsync(user.Id);
        var meeting2 = await CreateMeetingAsync(user.Id);

        // Create a valid organizer for the other meeting
        var otherOrganizer = await CreateUserAsync("other@test.com");
        var otherMeeting = await CreateMeetingAsync(otherOrganizer.Id);

        _dbContext.MeetingParticipants.Add(new MeetingParticipant { MeetingId = meeting1.Id, UserId = user.Id });
        _dbContext.MeetingParticipants.Add(new MeetingParticipant { MeetingId = meeting2.Id, UserId = user.Id });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _meetingService.GetUserMeetingsAsync(user.Id, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.Select(m => m.Id).Should().Contain(new[] { meeting1.Id, meeting2.Id });
    }

    /// <summary>
    /// Verifies a successful meeting update by the organizer.
    /// </summary>
    [Test]
    public async Task UpdateMeetingAsync_WhenUserIsOrganizer_SucceedsAndReturnsUpdatedDto()
    {
        // Arrange
        var organizer = await CreateUserAsync("org@test.com");
        var meeting = await CreateMeetingAsync(organizer.Id);

        // An organizer is always a participant. We must add this record for the
        // GetMeetingByIdAsync call to succeed.
        _dbContext.MeetingParticipants.Add(new MeetingParticipant { MeetingId = meeting.Id, UserId = organizer.Id });
        await _dbContext.SaveChangesAsync();

        var dto = new UpdateMeetingDto("Updated Name", "Updated Desc", DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddHours(1), ["org@test.com", "nonorg@test.com"]);

        // Act
        var (updatedMeeting, errorMessage) = await _meetingService.UpdateMeetingAsync(meeting.Id, dto, organizer.Id, CancellationToken.None);

        // Assert
        errorMessage.Should().BeNull();
        updatedMeeting.Should().NotBeNull();
        updatedMeeting!.Name.Should().Be("Updated Name");

        var meetingInDb = await _dbContext.Meetings.FindAsync(meeting.Id);
        meetingInDb!.Name.Should().Be("Updated Name");
    }

    /// <summary>
    /// Verifies a successful meeting cancellation by the organizer.
    /// </summary>
    [Test]
    public async Task CancelMeetingAsync_WhenUserIsOrganizer_SucceedsAndReturnsTrue()
    {
        // Arrange
        var organizer = await CreateUserAsync("org@test.com");
        var meeting = await CreateMeetingAsync(organizer.Id);

        // Act
        var result = await _meetingService.CancelMeetingAsync(meeting.Id, organizer.Id, CancellationToken.None);

        // Assert
        result.Status.Should().BeTrue();
        var meetingInDb = await _dbContext.Meetings.FindAsync(meeting.Id);
        meetingInDb!.IsCanceled.Should().BeTrue();
        meetingInDb.CanceledAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// Verifies that a participant can be successfully added to a meeting.
    /// </summary>
    [Test]
    public async Task AddParticipantAsync_WithValidUser_SucceedsAndReturnsTrue()
    {
        // Arrange
        var organizer = await CreateUserAsync("org@test.com");
        var userToAdd = await CreateUserAsync("new@test.com");
        var meeting = await CreateMeetingAsync(organizer.Id);

        // Act
        var result = await _meetingService.AddParticipantAsync(meeting.Id, userToAdd.Email, organizer.Id, CancellationToken.None);

        // Assert
        result.Status.Should().BeTrue();
        (await _dbContext.MeetingParticipants.AnyAsync(p => p.MeetingId == meeting.Id && p.UserId == userToAdd.Id)).Should().BeTrue();
    }

    /// <summary>
    /// Verifies that adding an existing participant is idempotent and returns true.
    /// </summary>
    [Test]
    public async Task AddParticipantAsync_WhenParticipantAlreadyExists_ReturnsTrue()
    {
        // Arrange
        var organizer = await CreateUserAsync("org@test.com");
        var existingParticipant = await CreateUserAsync("existing@test.com");
        var meeting = await CreateMeetingAsync(organizer.Id);
        _dbContext.MeetingParticipants.Add(new MeetingParticipant { MeetingId = meeting.Id, UserId = existingParticipant.Id });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _meetingService.AddParticipantAsync(meeting.Id, existingParticipant.Email, organizer.Id, CancellationToken.None);

        // Assert
        result.Status.Should().BeTrue();
    }

    /// <summary>
    /// Verifies that an organizer cannot remove themselves from a meeting.
    /// </summary>
    [Test]
    public async Task RemoveParticipantAsync_WhenOrganizerRemovesSelf_ReturnsFalse()
    {
        // Arrange
        var organizer = await CreateUserAsync("org@test.com");
        var meeting = await CreateMeetingAsync(organizer.Id);
        _dbContext.MeetingParticipants.Add(new MeetingParticipant { MeetingId = meeting.Id, UserId = organizer.Id });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _meetingService.RemoveParticipantAsync(meeting.Id, organizer.Id, organizer.Id, CancellationToken.None);

        // Assert
        result.Status.Should().BeFalse();
    }

    /// <summary>
    /// Verifies a successful removal of a participant by the organizer.
    /// </summary>
    [Test]
    public async Task RemoveParticipantAsync_WithValidParticipant_SucceedsAndReturnsTrue()
    {
        // Arrange
        var organizer = await CreateUserAsync("org@test.com");
        var participantToRemove = await CreateUserAsync("toremove@test.com");
        var meeting = await CreateMeetingAsync(organizer.Id);
        _dbContext.MeetingParticipants.Add(new MeetingParticipant { MeetingId = meeting.Id, UserId = participantToRemove.Id });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _meetingService.RemoveParticipantAsync(meeting.Id, participantToRemove.Id, organizer.Id, CancellationToken.None);

        // Assert
        result.Status.Should().BeTrue();
        (await _dbContext.MeetingParticipants.AnyAsync(p => p.UserId == participantToRemove.Id)).Should().BeFalse();
    }

    /// <summary>
    /// Verifies that CreateMeetingAsync throws an exception if a database error occurs during commit.
    /// </summary>
    [Test]
    public async Task CreateMeetingAsync_WhenCommitFails_ThrowsAndRollsBack()
    {
        // Arrange
        var organizerId = Guid.NewGuid();
        var organizer = new User { Id = organizerId, Email = "organizer@test.com", FirstName = "Org", LastName = "User", Phone = "123", PasswordHash = "hash" };
        var dto = new CreateMeetingDto("Test", "Desc", DateTime.UtcNow, DateTime.UtcNow.AddHours(1), null);

        var users = new List<User> { organizer };
        var meetings = new List<Meeting>();
        var participants = new List<MeetingParticipant>();

        var mockUserDbSet = users.BuildMockDbSet();
        var mockMeetingDbSet = meetings.BuildMockDbSet();
        var mockParticipantDbSet = participants.BuildMockDbSet();

        var mockDbContext = new Mock<MeetingSystemDbContext>();
        mockDbContext.Setup(c => c.Users).Returns(mockUserDbSet.Object);
        mockDbContext.Setup(c => c.Meetings).Returns(mockMeetingDbSet.Object);
        mockDbContext.Setup(c => c.MeetingParticipants).Returns(mockParticipantDbSet.Object);
        mockDbContext.Setup(c => c.Set<User>()).Returns(mockUserDbSet.Object);
        mockDbContext.Setup(c => c.Set<Meeting>()).Returns(mockMeetingDbSet.Object);
        mockDbContext.Setup(c => c.Set<MeetingParticipant>()).Returns(mockParticipantDbSet.Object);

        var mockTransaction = new Mock<IDbContextTransaction>();
        var mockDatabaseFacade = new Mock<DatabaseFacade>(mockDbContext.Object);
        mockDatabaseFacade.Setup(db => db.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockTransaction.Object);
        mockDbContext.Setup(c => c.Database).Returns(mockDatabaseFacade.Object);

        mockDbContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("Simulated DB error during commit."));

        var unitOfWork = new UnitOfWork(mockDbContext.Object, Mock.Of<ILogger<UnitOfWork>>());
        var meetingService = new MeetingService(unitOfWork, _backgroundJobClientMock.Object, _loggerMock.Object);

        // Act
        Func<Task> act = async () => await meetingService.CreateMeetingAsync(dto, organizer.Id, true, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    /// <summary>
    /// Verifies that CreateMeetingAsync works correctly when no extra participants are provided.
    /// </summary>
    [Test]
    public async Task CreateMeetingAsync_WithNullParticipantEmails_Succeeds()
    {
        // Arrange
        var organizer = await CreateUserAsync("organizer@test.com");
        var dto = new CreateMeetingDto("Solo Meeting", "Desc", DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddHours(2), null); // ParticipantEmails is null

        // Act
        var resultDto = await _meetingService.CreateMeetingAsync(dto, organizer.Id, true, CancellationToken.None);

        // Assert
        resultDto.Should().NotBeNull();
        resultDto!.Participants.Should().HaveCount(1); // Only the organizer
        resultDto.Participants.First().UserId.Should().Be(organizer.Id);
    }

    /// <summary>
    /// Verifies that AddParticipantAsync fails gracefully if the meeting does not exist.
    /// </summary>
    [Test]
    public async Task AddParticipantAsync_WhenMeetingNotFound_ReturnsFalse()
    {
        // Arrange
        var organizer = await CreateUserAsync("org@test.com");
        var userToAdd = await CreateUserAsync("new@test.com");
        var nonExistentMeetingId = Guid.NewGuid();

        // Act
        var result = await _meetingService.AddParticipantAsync(nonExistentMeetingId, userToAdd.Email, organizer.Id, CancellationToken.None);

        // Assert
        result.Status.Should().BeFalse();
    }

    /// <summary>
    /// Verifies that RemoveParticipantAsync fails gracefully if the meeting does not exist.
    /// </summary>
    [Test]
    public async Task RemoveParticipantAsync_WhenMeetingNotFound_ReturnsFalse()
    {
        // Arrange
        var organizer = await CreateUserAsync("org@test.com");
        var userToRemove = await CreateUserAsync("toremove@test.com");
        var nonExistentMeetingId = Guid.NewGuid();

        // Act
        var result = await _meetingService.RemoveParticipantAsync(nonExistentMeetingId, userToRemove.Id, organizer.Id, CancellationToken.None);

        // Assert
        result.Status.Should().BeFalse();
    }

    /// <summary>
    /// Verifies that CreateMeetingAsync throws an exception if the organizer ID does not exist in the database.
    /// </summary>
    [Test]
    public async Task CreateMeetingAsync_WhenOrganizerDoesNotExist_ThrowsInvalidOperationException()
    {
        // Arrange
        var nonExistentOrganizerId = Guid.NewGuid();
        var participant = await CreateUserAsync("participant@test.com");
        var dto = new CreateMeetingDto(
            "Test Meeting",
            "Description",
            DateTime.UtcNow,
            DateTime.UtcNow.AddHours(1),
            new[] { participant.Email });

        // Act
        Func<Task> act = async () => await _meetingService.CreateMeetingAsync(dto, nonExistentOrganizerId, true, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
                 .WithMessage($"Organizer with ID {nonExistentOrganizerId} not found.");

        (await _dbContext.Meetings.CountAsync()).Should().Be(0);
    }

    [Test]
    public async Task UpdateMeetingAsync_WhenCommitFails_Throws()
    {
        // Arrange
        var organizer = await CreateUserAsync("org@test.com");
        var meeting = await CreateMeetingAsync(organizer.Id);
        var dto = new UpdateMeetingDto("New Name", "New Desc", DateTime.UtcNow, DateTime.UtcNow.AddHours(1), new[] { "org@test.com", "nonorg@test.com" });

        var mockUnitOfWork = new Mock<IUnitOfWork>();
        mockUnitOfWork.Setup(u => u.Meetings).Returns(_unitOfWork.Meetings);
        mockUnitOfWork.Setup(u => u.Users).Returns(_unitOfWork.Users);
        mockUnitOfWork.Setup(u => u.MeetingParticipants).Returns(_unitOfWork.MeetingParticipants);

        mockUnitOfWork.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(Mock.Of<IDbContextTransaction>()));

        mockUnitOfWork.Setup(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Simulated commit failure"));

        mockUnitOfWork.Setup(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var meetingService = new MeetingService(mockUnitOfWork.Object, _backgroundJobClientMock.Object, _loggerMock.Object);

        // Act
        Func<Task> act = () => meetingService.UpdateMeetingAsync(meeting.Id, dto, organizer.Id, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<Exception>().WithMessage("Simulated commit failure");
        mockUnitOfWork.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verifies that CancelMeetingAsync returns a failure when the target meeting does not exist.
    /// </summary>
    [Test]
    public async Task CancelMeetingAsync_WhenMeetingNotFound_ReturnsFailure()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var nonExistentMeetingId = Guid.NewGuid();

        // Act
        var (status, message) = await _meetingService.CancelMeetingAsync(nonExistentMeetingId, userId, CancellationToken.None);

        // Assert
        status.Should().BeFalse();
        message.Should().Be("Meeting not found.");
    }

    /// <summary>
    /// Verifies that a non-organizer cannot remove a participant.
    /// </summary>
    [Test]
    public async Task RemoveParticipantAsync_WhenUserIsNotOrganizer_ReturnsFailure()
    {
        // Arrange
        var organizer = await CreateUserAsync("org@test.com");
        var participant = await CreateUserAsync("participant@test.com");
        var nonOrganizer = await CreateUserAsync("non-organizer@test.com");
        var meeting = await CreateMeetingAsync(organizer.Id);
        _dbContext.MeetingParticipants.AddRange(new MeetingParticipant { MeetingId = meeting.Id, UserId = organizer.Id }, new MeetingParticipant { MeetingId = meeting.Id, UserId = participant.Id });
        await _dbContext.SaveChangesAsync();

        // Act
        var (status, message) = await _meetingService.RemoveParticipantAsync(meeting.Id, participant.Id, nonOrganizer.Id, CancellationToken.None);

        // Assert
        status.Should().BeFalse();
        message.Should().Be("User is not authorized to remove participants from this meeting.");
    }

    // ***** NEW TEST CASE TO COVER MISSING BRANCH *****
    /// <summary>
    /// Verifies that a non-organizer cannot add a participant to a meeting.
    /// </summary>
    [Test]
    public async Task AddParticipantAsync_WhenUserIsNotOrganizer_ReturnsFailure()
    {
        // Arrange
        var organizer = await CreateUserAsync("org@test.com");
        var nonOrganizer = await CreateUserAsync("non-organizer@test.com");
        var userToAdd = await CreateUserAsync("new-user@test.com");
        var meeting = await CreateMeetingAsync(organizer.Id);

        // Act: The non-organizer is attempting the action.
        var (status, message) = await _meetingService.AddParticipantAsync(meeting.Id, userToAdd.Email, nonOrganizer.Id, CancellationToken.None);

        // Assert
        status.Should().BeFalse();
        message.Should().Be("User is not authorized to add participants to this meeting.");
    }

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

    #endregion Helper Methods
}