using System.ComponentModel.DataAnnotations;

namespace MeetingSystem.Model;

/// <summary>
/// Represents a JWT that has been revoked (blacklisted) via the logout process.
/// </summary>
public class RevokedToken
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RevokedToken"/> class.
    /// </summary>
    public RevokedToken() { }

    /// <summary>
    /// The unique identifier for the revoked token record.
    /// </summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// The 'jti' (JWT ID) claim of the token being revoked. This is indexed for fast lookups.
    /// </summary>
    [Required]
    public required string Jti { get; set; }

    /// <summary>
    /// The UTC timestamp when the token was revoked.
    /// </summary>
    public DateTime RevokedAt { get; set; }
}