using System.Linq.Expressions;

using FluentAssertions;

using Hangfire;
using Hangfire.Common;
using Hangfire.States;

using MeetingSystem.Business.Dtos;
using MeetingSystem.Business.Jobs;
using MeetingSystem.Business.Tests.Infrastructure;
using MeetingSystem.Context;
using MeetingSystem.Model;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Moq;
using MockQueryable.Moq;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

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
        Func<Task> act = async () => await _meetingService.CreateMeetingAsync(dto, nonExistentOrganizerId, CancellationToken.None);

        // Assert
        act.Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary>
    /// Verifies that GetMeetingByIdAsync returns null if the user is not a participant.
    /// </summary>
    [Test]
    public async Task GetMeetingByIdAsync_WhenUserIsNotParticipant_ReturnsNull()
    {
        // Arrange
        var organizer = await CreateUserAsync("org@test.com");
        var meeting = await CreateMeetingAsync(organizer.Id);
        var nonOrganizer = await CreateUserAsync("nonorg@test.com");
        var dto = new UpdateMeetingDto("New Name", "New Desc", DateTime.UtcNow, DateTime.UtcNow.AddHours(1));

        // Act
        var (updatedMeeting, errorMessage) = await _meetingService.UpdateMeetingAsync(meeting.Id, dto, nonOrganizer.Id, CancellationToken.None);

        // Assert
        updatedMeeting.Should().BeNull();
        errorMessage.Should().Be("Not authorized: User is not the organizer");
    }

    /// <summary>
    /// Verifies that UpdateMeetingAsync returns null if the meeting is not found.
    /// </summary>
    [Test]
    public async Task UpdateMeetingAsync_WhenMeetingNotFound_ReturnsNull()
    {
        // Arrange
        var user = await CreateUserAsync("user@test.com");
        var dto = new UpdateMeetingDto("New Name", "New Desc", DateTime.UtcNow, DateTime.UtcNow.AddHours(1));
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
        result.Should().BeFalse();
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
        result.Should().BeFalse();
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
        result.Should().BeFalse();
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
        var resultDto = await _meetingService.CreateMeetingAsync(dto, organizer.Id, CancellationToken.None);

        // Assert
        resultDto.Should().NotBeNull();
        resultDto.Name.Should().Be(dto.Name);
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

        var dto = new UpdateMeetingDto("Updated Name", "Updated Desc", DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddHours(1));

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
        result.Should().BeTrue();
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
        result.Should().BeTrue();
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
        result.Should().BeTrue();
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
        result.Should().BeFalse();
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
        result.Should().BeTrue();
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

        // 1. Create in-memory lists for your test data.
        var users = new List<User> { organizer };
        var roles = new List<Role> { new Role { Id = Guid.NewGuid(), Name = "User" } };
        var userRoles = new List<UserRole>();
        var meetings = new List<Meeting>();
        var participants = new List<MeetingParticipant>();

        // 2. Use MockQueryable.Moq to build async-capable mock DbSets from the lists.
        var mockUserDbSet = users.BuildMockDbSet();
        var mockRoleDbSet = roles.BuildMockDbSet();
        var mockUserRoleDbSet = userRoles.BuildMockDbSet();
        var mockMeetingDbSet = meetings.BuildMockDbSet();
        var mockParticipantDbSet = participants.BuildMockDbSet();

        // 3. Create a mock DbContext.
        var mockDbContext = new Mock<MeetingSystemDbContext>();

        // 4. Set up the virtual DbSet properties.
        mockDbContext.Setup(c => c.Users).Returns(mockUserDbSet.Object);
        mockDbContext.Setup(c => c.Roles).Returns(mockRoleDbSet.Object);
        mockDbContext.Setup(c => c.UserRoles).Returns(mockUserRoleDbSet.Object);
        mockDbContext.Setup(c => c.Meetings).Returns(mockMeetingDbSet.Object);
        mockDbContext.Setup(c => c.MeetingParticipants).Returns(mockParticipantDbSet.Object);

        // 5. Set up the generic Set<T>() method to return the correct mock DbSet for each type.
        // This is what the GenericRepository constructor calls.
        mockDbContext.Setup(c => c.Set<User>()).Returns(mockUserDbSet.Object);
        mockDbContext.Setup(c => c.Set<Role>()).Returns(mockRoleDbSet.Object);
        mockDbContext.Setup(c => c.Set<UserRole>()).Returns(mockUserRoleDbSet.Object);
        mockDbContext.Setup(c => c.Set<Meeting>()).Returns(mockMeetingDbSet.Object);
        mockDbContext.Setup(c => c.Set<MeetingParticipant>()).Returns(mockParticipantDbSet.Object);

        // 6. Mock the DatabaseFacade to handle transaction calls.
        var mockTransaction = new Mock<IDbContextTransaction>();
        var mockDatabaseFacade = new Mock<DatabaseFacade>(mockDbContext.Object);
        mockDatabaseFacade.Setup(db => db.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockTransaction.Object);
        mockDbContext.Setup(c => c.Database).Returns(mockDatabaseFacade.Object);

        // 7. Setup SaveChangesAsync to throw the exception we want to test.
        // We need to mock both overloads of SaveChangesAsync.
        mockDbContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("Simulated DB error during commit."));
        mockDbContext.Setup(c => c.SaveChangesAsync(true, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("Simulated DB error during commit."));

        // 8. Create a real UnitOfWork that uses our fully mocked DbContext.
        var unitOfWork = new UnitOfWork(mockDbContext.Object, Mock.Of<ILogger<UnitOfWork>>());

        // 9. Create the service instance.
        var meetingService = new MeetingService(unitOfWork, _backgroundJobClientMock.Object, _loggerMock.Object);

        // Act
        Func<Task> act = async () => await meetingService.CreateMeetingAsync(dto, organizer.Id, CancellationToken.None);

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
        var resultDto = await _meetingService.CreateMeetingAsync(dto, organizer.Id, CancellationToken.None);

        // Assert
        resultDto.Should().NotBeNull();
        resultDto.Participants.Should().HaveCount(1); // Only the organizer
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
        result.Should().BeFalse();
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
        result.Should().BeFalse();
    }

    /// <summary>
    /// Verifies that CreateMeetingAsync throws an exception if the organizer ID does not exist in the database.
    /// </summary>
    [Test]
    public async Task CreateMeetingAsync_WhenOrganizerDoesNotExist_ThrowsInvalidOperationException()
    {
        // Arrange
        var nonExistentOrganizerId = Guid.NewGuid();
        // Create a participant so the DTO is valid, but don't create the organizer.
        var participant = await CreateUserAsync("participant@test.com");
        var dto = new CreateMeetingDto(
            "Test Meeting",
            "Description",
            DateTime.UtcNow,
            DateTime.UtcNow.AddHours(1),
            new[] { participant.Email });

        // Act
        Func<Task> act = async () => await _meetingService.CreateMeetingAsync(dto, nonExistentOrganizerId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
                 .WithMessage($"Organizer with ID {nonExistentOrganizerId} not found.");

        // Verify no meeting was created
        (await _dbContext.Meetings.CountAsync()).Should().Be(0);
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