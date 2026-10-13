namespace Api.Validators;

using FluentValidation;
using Api.Application.Contracts;
using Domain;

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public const int MinPasswordLength = 12;
    public const int MaxPasswordLength = 128;
    public const int MaxEmailLength = 256;

    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email must be a valid address, e.g. name@example.com.")
            .MaximumLength(MaxEmailLength).WithMessage($"Email cannot exceed {MaxEmailLength} characters.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(MinPasswordLength)
                .WithMessage(x => $"Password must be at least {MinPasswordLength} characters (you sent {x.Password?.Length ?? 0}).")
            .MaximumLength(MaxPasswordLength) // stops someone sending a 10MB "password" to burn CPU on hashing
                .WithMessage($"Password cannot exceed {MaxPasswordLength} characters.");

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Full name is required.")
            .MaximumLength(User.MaxFullNameLength)
                .WithMessage($"Full name cannot exceed {User.MaxFullNameLength} characters.");
    }
}
