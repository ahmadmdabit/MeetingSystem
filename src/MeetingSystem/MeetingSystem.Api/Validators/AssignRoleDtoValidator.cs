using FluentValidation;

using MeetingSystem.Business.Dtos;

namespace MeetingSystem.Api.Validators
{
    public class AssignRoleDtoValidator : AbstractValidator<AssignRoleDto>
    {
        public AssignRoleDtoValidator()
        {
            RuleFor(x => x.RoleName).NotEmpty().WithMessage("Role name cannot be empty.");
        }
    }
}
