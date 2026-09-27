namespace TallyVel.Api.Domain;

using FluentValidation;
using TallyVel.Api.Application.Contracts;

public class ChangeStokvelMemberRoleValidator : AbstractValidator<ChangeStokvelMemberRoleRequest>
{
    public ChangeStokvelMemberRoleValidator()
    {
        RuleFor(x => x.Role)
            .IsInEnum()
            .WithMessage("Add a valid role");
    }
}
