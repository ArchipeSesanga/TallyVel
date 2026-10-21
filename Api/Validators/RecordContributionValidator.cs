namespace Api.Validators;

using FluentValidation;
using Api.Application.Contracts;
using Domain;

public class RecordContributionValidator : AbstractValidator<RecordContributionRequest>
{
    public RecordContributionValidator()
    {
        RuleFor(x => x.MemberUserId)
            .NotEmpty()
            .WithMessage("MemberUserId is required");

        RuleFor(x => x.Cycle)
            .NotEmpty()
            .WithMessage("Cycle is required")
            .MaximumLength(ContributionCycle.MaxLabelLength)
            .WithMessage($"Cycle cannot exceed {ContributionCycle.MaxLabelLength} characters");

        RuleFor(x => x.AmountCents)
            .GreaterThan(0)
            .WithMessage("Contribution amount must be bigger than 0 cents")
            .LessThanOrEqualTo(Money.MaxCents)
            .WithMessage($"Contribution amount cannot exceed {Money.MaxCents} cents");
    }
}
