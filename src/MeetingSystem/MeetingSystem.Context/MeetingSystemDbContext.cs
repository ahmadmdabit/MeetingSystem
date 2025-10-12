using MeetingSystem.Model;

using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace MeetingSystem.Context;

/// <summary>
/// The Entity Framework Core database context for the MeetingSystem application.
/// </summary>
public class MeetingSystemDbContext : DbContext, IDataProtectionKeyContext
{
    public MeetingSystemDbContext(DbContextOptions<MeetingSystemDbContext> options) : base(options) { }
    
    /// <summary>
    /// Represents the collection of keys used by the ASP.NET Core Data Protection system.
    /// </summary>
    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; }

    public DbSet<User> Users { get; set; }
    public DbSet<Role> Roles { get; set; }
    public DbSet<UserRole> UserRoles { get; set; }
    public DbSet<Meeting> Meetings { get; set; }
    public DbSet<MeetingFile> MeetingFiles { get; set; }
    public DbSet<MeetingParticipant> MeetingParticipants { get; set; }
    public DbSet<MeetingsLog> MeetingsLog { get; set; }
    public DbSet<RevokedToken> RevokedTokens { get; set; }

    /// <summary>
    /// Configures the schema and relationships for the database model using the Fluent API.
    /// </summary>
    /// <param name="modelBuilder">The builder being used to construct the model for this context.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ...... User Configuration ......
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.FirstName).HasMaxLength(100);
            entity.Property(u => u.LastName).HasMaxLength(100);
            entity.Property(u => u.Email).HasMaxLength(256);
            entity.Property(u => u.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
        });

        // ...... Role Configuration ......
        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("Roles");
            entity.HasIndex(r => r.Name).IsUnique(); // Role names must be unique
            entity.Property(r => r.Name).HasMaxLength(256);
        });

        // ...... UserRole Configuration ......
        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.ToTable("UserRoles");
            entity.HasKey(ur => new { ur.UserId, ur.RoleId }); // Composite primary key

            // Relationship to User
            entity.HasOne(ur => ur.User)
                .WithMany() // A User can have many UserRole entries
                .HasForeignKey(ur => ur.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Relationship to Role
            entity.HasOne(ur => ur.Role)
                .WithMany(r => r.UserRoles)
                .HasForeignKey(ur => ur.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ...... Meeting Configuration ......
        modelBuilder.Entity<Meeting>(entity =>
        {
            entity.ToTable("Meetings");
            entity.Property(m => m.Name).HasMaxLength(200);
            entity.Property(m => m.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

            // Configure the one-to-many relationship between User (Organizer) and Meeting
            entity.HasOne(m => m.Organizer)
                  .WithMany(u => u.OrganizedMeetings)
                  .HasForeignKey(m => m.OrganizerId)
                  .OnDelete(DeleteBehavior.Restrict); // Prevent deleting a user if they organize meetings
        });

        // ...... MeetingParticipant (Many-to-Many Join Table) Configuration ......
        modelBuilder.Entity<MeetingParticipant>(entity =>
        {
            entity.ToTable("MeetingParticipants");
            entity.HasKey(mp => new { mp.MeetingId, mp.UserId }); // Composite primary key

            // Relationship to Meeting
            entity.HasOne(mp => mp.Meeting)
                  .WithMany(m => m.Participants)
                  .HasForeignKey(mp => mp.MeetingId)
                  .OnDelete(DeleteBehavior.Cascade); // If a meeting is deleted, remove participants

            // Relationship to User
            entity.HasOne(mp => mp.User)
                  .WithMany(u => u.Meetings)
                  .HasForeignKey(mp => mp.UserId)
                  .OnDelete(DeleteBehavior.Cascade); // If a user is deleted, remove them from meetings

            entity.Property(mp => mp.AddedAt).HasDefaultValueSql("GETUTCDATE()");
        });

        // ...... MeetingFile Configuration ......
        modelBuilder.Entity<MeetingFile>(entity =>
        {
            entity.ToTable("MeetingFiles");
            entity.Property(f => f.FileName).HasMaxLength(255);
            entity.Property(f => f.ContentType).HasMaxLength(100);
            entity.Property(f => f.UploadedAt).HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(f => f.Meeting)
                  .WithMany(m => m.Files)
                  .HasForeignKey(f => f.MeetingId)
                  .OnDelete(DeleteBehavior.Cascade); // If a meeting is deleted, delete its files
        });

        // ...... MeetingsLog Configuration ......
        modelBuilder.Entity<MeetingsLog>(entity =>
        {
            entity.ToTable("LogMeetings");
            entity.Property(l => l.DeletedAt).HasDefaultValueSql("GETUTCDATE()");
        });

        // ...... RevokedToken Configuration ......
        modelBuilder.Entity<RevokedToken>(entity =>
        {
            entity.ToTable("RevokedTokens");
            entity.HasIndex(rt => rt.Jti); // Index JTI for fast lookups
            entity.Property(rt => rt.RevokedAt).HasDefaultValueSql("GETUTCDATE()");
        });
    }
}