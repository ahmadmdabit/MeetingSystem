using FluentValidation;
using MeetingSystem.Business.Dtos;

namespace MeetingSystem.Api.Validators
{
    public class AddParticipantDtoValidator : AbstractValidator<AddParticipantDto>
    {
        public AddParticipantDtoValidator()
        {
            RuleFor(x => x.Email).NotEmpty().EmailAddress();
        }
    }
}
