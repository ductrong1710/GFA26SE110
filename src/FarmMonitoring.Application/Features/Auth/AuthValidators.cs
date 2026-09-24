using FluentValidation;

namespace FarmMonitoring.Application.Features.Auth;

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).Cascade(CascadeMode.Stop).NotEmpty().MaximumLength(255)
            .Must(email => new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email.Trim()))
            .WithMessage("A valid email address is required.").OverridePropertyName("email");
        RuleFor(x => x.Password).NotEmpty().MaximumLength(1024).OverridePropertyName("password");
    }
}

public sealed class RefreshRequestValidator : AbstractValidator<RefreshRequest>
{
    public RefreshRequestValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty().MaximumLength(512).OverridePropertyName("refreshToken");
    }
}
