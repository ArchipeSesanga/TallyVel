namespace Api.Validators;

using FluentValidation;
using Api.Application.Contracts;
using Domain;

public class OpenCycleValidator : AbstractValidator<OpenCycleRequest>
{
    public OpenCycleValidator()
    {
        RuleFor(x => x.Label)
            .NotEmpty().WithMessage("Label is required, e.g. 2026-10.")
            .MaximumLength(ContributionCycle.MaxLabelLength)
                .WithMessage($"Label cannot exceed {ContributionCycle.MaxLabelLength} characters.");
    }
}

public class PayoutValidator : AbstractValidator<PayoutRequest>
{
    public PayoutValidator()
    {
        RuleFor(x => x.RecipientUserId).NotEmpty().WithMessage("RecipientUserId is required.");
    }
}
