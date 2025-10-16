using Bogus;

using MeetingSystem.Business.Configuration;
using MeetingSystem.Context;
using MeetingSystem.Model;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MeetingSystem.Api;

public static class DataSeeder
{
    public static async Task SeedDataAsync(IServiceProvider serviceProvider, ILogger logger)
    {
        var seedingSettings = serviceProvider.GetRequiredService<IOptions<SeedingSettings>>().Value;
        if (!seedingSettings.Enabled)
        {
            logger.LogInformation("Seeding is disabled. Skipping.");
            return;
        }

        logger.LogInformation("Starting database seeding......");

        var unitOfWork = serviceProvider.GetRequiredService<IUnitOfWork>();
        var passwordHasher = serviceProvider.GetRequiredService<IPasswordHasher<User>>();

        // 1. Ensure Roles Exist
        logger.LogInformation("Database seeding 1/3");
        var adminRole = await EnsureRoleExistsAsync(unitOfWork, "Admin", logger);
        var userRole = await EnsureRoleExistsAsync(unitOfWork, "User", logger);
        await unitOfWork.CompleteAsync();

        // 2. Seed Explicit Users (Admin + Default)
        logger.LogInformation("Database seeding 2/3");
        var existingUserEmails = await unitOfWork.Users.GetAll().Select(u => u.Email).ToListAsync();
        var createdUsers = new List<User>();

        if (seedingSettings.AdminUser != null && !existingUserEmails.Contains(seedingSettings.AdminUser.Email))
        {
            createdUsers.Add(CreateUser(seedingSettings.AdminUser, passwordHasher, adminRole));
        }

        if (seedingSettings.DefaultUsers != null)
        {
            foreach (var userSeed in seedingSettings.DefaultUsers.Where(u => !existingUserEmails.Contains(u.Email)))
            {
                createdUsers.Add(CreateUser(userSeed, passwordHasher, userRole));
            }
        }

        // 3. Seed Bogus
        logger.LogInformation("Database seeding 3/3");
        if (seedingSettings.BogusData?.Generate == true)
        {
            // 3.1. Seed Bogus Users
            logger.LogInformation("Database seeding 3.1/3");
            if (seedingSettings.BogusData.UserCount > 0)
            {
                var userFaker = new Faker<User>()
                    .RuleFor(u => u.Id, f => Guid.NewGuid())
                    .RuleFor(u => u.FirstName, f => f.Name.FirstName())
                    .RuleFor(u => u.LastName, f => f.Name.LastName())
                    .RuleFor(u => u.Email, (f, u) => f.Internet.Email(u.FirstName, u.LastName))
                    .RuleFor(u => u.Phone, f => f.Phone.PhoneNumber())
                    .RuleFor(u => u.PasswordHash, f => passwordHasher.HashPassword(null!, "Password!123"));

                var bogusUsers = userFaker.Generate(seedingSettings.BogusData.UserCount);
                foreach (var user in bogusUsers)
                {
                    user.UserRoles.Add(new UserRole { Role = userRole });
                    createdUsers.Add(user);
                }
            }

            if (createdUsers.Count != 0)
            {
                unitOfWork.Users.AddRange(createdUsers);
                await unitOfWork.CompleteAsync();
                logger.LogInformation("Seeded {Count} new users.", createdUsers.Count);
            }

            // 3.2. Seed Bogus Meetings
            logger.LogInformation("Database seeding 3.2/3");
            if (seedingSettings.BogusData.MeetingCount > 0)
            {
                var allUserIds = await unitOfWork.Users.GetAll().Select(u => u.Id).ToListAsync();
                if (allUserIds.Count < 2)
                {
                    logger.LogWarning("Not enough users in the database to seed meetings. Skipping.");
                    return;
                }

                var durations = new[] { 15, 30, 45, 60, 90, 120 };

                var meetingFaker = new Faker<Meeting>()
                    .RuleFor(m => m.Id, f => Guid.NewGuid())
                    .RuleFor(m => m.Name, f => f.Company.Bs())
                    .RuleFor(m => m.Description, f => f.Lorem.Sentence(10))
                    .RuleFor(m => m.OrganizerId, f => f.PickRandom(allUserIds))
                    .RuleFor(m => m.StartAt, f => f.Date.Future(0, DateTime.UtcNow))
                    .RuleFor(m => m.EndAt, (f, m) => m.StartAt.AddMinutes(f.Random.ArrayElement(durations)));

                var bogusMeetings = meetingFaker.Generate(seedingSettings.BogusData.MeetingCount);

                foreach (var meeting in bogusMeetings)
                {
                    var participantCount = new Random().Next(2, Math.Min(allUserIds.Count, 7));
                    var participants = allUserIds.OrderBy(x => Guid.NewGuid()).Take(participantCount).ToList();
                    if (!participants.Contains(meeting.OrganizerId)) participants.Add(meeting.OrganizerId);

                    foreach (var userId in participants)
                    {
                        meeting.Participants.Add(new MeetingParticipant
                        {
                            UserId = userId,
                            Role = userId == meeting.OrganizerId ? "Organizer" : "Participant"
                        });
                    }
                }

                unitOfWork.Meetings.AddRange(bogusMeetings);
                await unitOfWork.CompleteAsync();
                logger.LogInformation("Seeded {Count} new meetings.", bogusMeetings.Count);
            }
        }
        logger.LogInformation("Database seeding finished.");
    }

    private static async Task<Role> EnsureRoleExistsAsync(IUnitOfWork unitOfWork, string roleName, ILogger logger)
    {
        var role = await unitOfWork.Roles.Find(r => r.Name == roleName).FirstOrDefaultAsync().ConfigureAwait(false);
        if (role == null)
        {
            logger.LogInformation("Seeding role: {RoleName}", roleName);
            role = new Role { Id = Guid.NewGuid(), Name = roleName };
            unitOfWork.Roles.Add(role);
        }
        return role;
    }

    private static User CreateUser(UserSeedSettings seed, IPasswordHasher<User> hasher, Role role)
    {
        var user = new User
        {
            Id = seed.Id,
            FirstName = seed.FirstName,
            LastName = seed.LastName,
            Email = seed.Email,
            Phone = "123456879",
            PasswordHash = string.Empty
        };
        user.PasswordHash = hasher.HashPassword(user, seed.Password);
        user.UserRoles.Add(new UserRole { Role = role });
        return user;
    }
}