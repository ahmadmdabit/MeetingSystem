using System.ComponentModel.DataAnnotations;

namespace MeetingSystem.Business.Configuration;

public class SeedingSettings
{
    public const string SectionName = "Seeding";
    public bool Enabled { get; init; }
    public UserSeedSettings? AdminUser { get; init; }
    public List<UserSeedSettings>? DefaultUsers { get; init; }
    public BogusDataSettings? BogusData { get; init; }
}

public class UserSeedSettings
{
    [Required] public required Guid Id { get; init; }
    [Required] public required string FirstName { get; init; }
    [Required] public required string LastName { get; init; }
    [Required, EmailAddress] public required string Email { get; init; }
    [Required] public required string Password { get; init; }
}

public class BogusDataSettings
{
    public bool Generate { get; init; }
    public int UserCount { get; init; }
    public int MeetingCount { get; init; }
    public required string DefaultPassword { get; init; }
}