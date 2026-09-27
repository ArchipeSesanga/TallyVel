namespace TallyVel.Api.Domain;

using FluentValidation;
using TallyVel.Api.Application.Contracts;

public class AddStokvelMemberValidator : AbstractValidator<AddStokvelMemberRequest>
{
    public AddStokvelMemberValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");

        RuleFor(x => x.Role)
            .IsInEnum()
            .WithMessage("Add a valid role");
    }
}
