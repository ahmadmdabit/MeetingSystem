using FluentValidation;

using MeetingSystem.Business.Dtos;

namespace MeetingSystem.Api.Validators
{
    public class UpdateUserProfileDtoValidator : AbstractValidator<UpdateUserProfileDto>
    {
        public UpdateUserProfileDtoValidator()
        {
            RuleFor(x => x.FirstName).NotEmpty().WithMessage("First name cannot be empty.");
            RuleFor(x => x.LastName).NotEmpty().WithMessage("Last name cannot be empty.");
            RuleFor(x => x.Phone).NotEmpty().WithMessage("Phone number cannot be empty.");
        }
    }
}
