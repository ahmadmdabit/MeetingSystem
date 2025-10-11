using FluentValidation;

using MeetingSystem.Business.Dtos;

namespace MeetingSystem.Api.Validators;

public class RegisterUserDtoValidator : AbstractValidator<RegisterUserDto>
{
    public RegisterUserDtoValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty();
        RuleFor(x => x.LastName).NotEmpty();
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty()
            .Matches(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*\W).{8,}$")
            .WithMessage("Password must be at least 8 characters long and contain an uppercase letter, a lowercase letter, a digit, and a special character.");
    }
}