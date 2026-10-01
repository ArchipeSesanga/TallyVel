namespace TallyVel.Api.Domain;

using FluentValidation;
using TallyVel.Api.Application.Contracts;


//string Name, decimal ContributionAmount, ContributionFrequency Cycle, Guid CreatorId
public class CreateStokVelValidator: AbstractValidator<CreateStokvelRequest>
{
    public CreateStokVelValidator()
{
    RuleFor(x => x.Name)
        .NotEmpty()
        .WithMessage("Name is required")
        .MaximumLength(Stokvel.MaxNameLength)
        .WithMessage($"Name cannot exceed {Stokvel.MaxNameLength} characters");

    RuleFor(x => x.ContributionAmount)
        .GreaterThan(0)
        .WithMessage("Contribution amount must be bigger than 0");

    RuleFor(x => x.Cycle)
        .IsInEnum()
        .WithMessage("Add a valid cycle");

    RuleFor(x => x.CreatorId)
        .NotEmpty()
        .WithMessage("CreatorId is required");
}

}