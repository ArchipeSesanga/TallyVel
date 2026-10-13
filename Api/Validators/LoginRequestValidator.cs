namespace Api.Validators;

using FluentValidation;
using Api.Application.Contracts;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .MaximumLength(RegisterRequestValidator.MaxEmailLength)
                .WithMessage($"Email cannot exceed {RegisterRequestValidator.MaxEmailLength} characters.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MaximumLength(RegisterRequestValidator.MaxPasswordLength) // same CPU-burn guard as registration
                .WithMessage($"Password cannot exceed {RegisterRequestValidator.MaxPasswordLength} characters.");
    }
}
