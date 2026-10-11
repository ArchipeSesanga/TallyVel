namespace Api.Validators;

using FluentValidation;
using Api.Application.Contracts;

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
