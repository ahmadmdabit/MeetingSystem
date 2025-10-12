using Microsoft.AspNetCore.Http;

namespace MeetingSystem.Business.Dtos;

/// <summary>
/// Represents the data required to authenticate a user.
/// </summary>
/// <param name="Email">The user's email address.</param>
/// <param name="Password">The user's plain-text password.</param>
public record LoginDto(string Email, string Password);

/// <summary>
/// Represents the data required to register a new user.
/// This is typically sent as multipart/form-data due to the potential file upload.
/// </summary>
/// <param name="FirstName">The user's first name.</param>
/// <param name="LastName">The user's last name.</param>
/// <param name="Email">The user's unique email address.</param>
/// <param name="Phone">The user's phone number.</param>
/// <param name="Password">The user's desired plain-text password.</param>
/// <param name="ProfilePicture">An optional profile picture file to upload.</param>
public record RegisterUserDto(
    string FirstName,
    string LastName,
    string Email,
    string Phone,
    string Password,
    IFormFile? ProfilePicture
);