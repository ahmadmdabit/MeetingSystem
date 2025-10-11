using Microsoft.AspNetCore.Http;

namespace MeetingSystem.Business.Dtos;

public record RegisterUserDto(
    string FirstName,
    string LastName,
    string Email,
    string Phone,
    string Password,
    IFormFile? ProfilePicture
);
