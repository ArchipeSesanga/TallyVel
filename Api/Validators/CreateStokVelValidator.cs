namespace Api.Validators;

using FluentValidation;
using Api.Application.Contracts;
using Domain;


//string Name, long ContributionAmountCents, ContributionFrequency Cycle, Guid CreatorId
public class CreateStokVelValidator: AbstractValidator<CreateStokvelRequest>
{
    public CreateStokVelValidator()
{
    RuleFor(x => x.Name)
        .NotEmpty()
        .WithMessage("Name is required")
        .MaximumLength(Stokvel.MaxNameLength)
        .WithMessage($"Name cannot exceed {Stokvel.MaxNameLength} characters");

    RuleFor(x => x.ContributionAmountCents)
        .GreaterThan(0)
        .WithMessage("Contribution amount must be bigger than 0 cents")
        .LessThanOrEqualTo(Money.MaxCents)
        .WithMessage($"Contribution amount cannot exceed {Money.MaxCents} cents");

    RuleFor(x => x.Cycle)
        .IsInEnum()
        .WithMessage("Add a valid cycle");

    RuleFor(x => x.CreatorId)
        .NotEmpty()
        .WithMessage("CreatorId is required");
}

}