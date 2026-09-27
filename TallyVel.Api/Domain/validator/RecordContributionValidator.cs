namespace TallyVel.Api.Domain;

using FluentValidation;
using TallyVel.Api.Application.Contracts;

public class RecordContributionValidator : AbstractValidator<RecordContributionRequest>
{
    public RecordContributionValidator()
    {
        RuleFor(x => x.MemberUserId)
            .NotEmpty()
            .WithMessage("MemberUserId is required");

        RuleFor(x => x.Cycle)
            .NotEmpty()
            .WithMessage("Cycle is required");

        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage("Contribution amount must be bigger than 0");
    }
}
