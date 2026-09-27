using FluentValidation;
using TallyVel.Api.Application.Contracts;
using TallyVel.Api.Domain;

public class CreateUserValidator: AbstractValidator<CreateUserRequest>
{
    public CreateUserValidator()
    {
        RuleFor(x => x.FullName)
              .NotEmpty().WithMessage("Full Name is required")
              .MaximumLength(User.MaxFullNameLength)
              .WithMessage($"Full Name cannot exceed {User.MaxFullNameLength} characters");

        RuleFor(x => x.Email)
              .NotEmpty()
              .WithMessage("Email is required")
              .EmailAddress()
              .WithMessage("Email is not a valid address");

        RuleFor(x => x.PasswordHash)
            .NotEmpty()
            .WithMessage("A password hash is required");
    }
}