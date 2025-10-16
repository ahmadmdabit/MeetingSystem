using FluentValidation;

using MeetingSystem.Business.Dtos;

namespace MeetingSystem.Api.Validators
{
    public class CreateMeetingDtoValidator : AbstractValidator<CreateMeetingDto>
    {
        public CreateMeetingDtoValidator()
        {
            RuleFor(x => x.Name).NotEmpty().WithMessage("Meeting name cannot be empty.");
            RuleFor(x => x.Description).NotEmpty().WithMessage("Description cannot be empty.");
            RuleFor(x => x.StartAt).NotEmpty().WithMessage("Start date cannot be empty.")
                .GreaterThanOrEqualTo(x => DateTime.UtcNow).WithMessage("Start date must be after now.");
            RuleFor(x => x.EndAt).NotEmpty().WithMessage("End date cannot be empty.")
                .GreaterThan(x => x.StartAt).WithMessage("End date must be after the start date.");
            RuleForEach(x => x.ParticipantEmails).EmailAddress().When(x => x.ParticipantEmails != null);
        }
    }
}
