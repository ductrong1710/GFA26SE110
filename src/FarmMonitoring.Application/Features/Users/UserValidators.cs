using FarmMonitoring.Domain.Constants;
using FluentValidation;

namespace FarmMonitoring.Application.Features.Users;

public sealed class UpdateUserValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserValidator()
    {
        RuleFor(x => x.Email).Cascade(CascadeMode.Stop).NotEmpty().MaximumLength(255)
            .Must(x => new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(x.Trim()))
            .WithMessage("A valid email address is required.").OverridePropertyName("email");
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(150).OverridePropertyName("fullName");
    }
}

public sealed class UserRolesValidator : AbstractValidator<UserRolesRequest>
{
    public UserRolesValidator()
    {
        RuleFor(x => x.Roles).Cascade(CascadeMode.Stop).NotNull()
            .Must(x => x.Distinct(StringComparer.Ordinal).Count() == x.Length)
            .WithMessage("Roles must be unique.").OverridePropertyName("roles");
        RuleForEach(x => x.Roles).Must(RoleNames.IsSupported)
            .WithMessage("Unknown role.").OverridePropertyName("roles");
    }
}

public sealed class CreateUserValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserValidator()
    {
        RuleFor(x => new UpdateUserRequest(x.Email, x.FullName)).SetValidator(new UpdateUserValidator());
        RuleFor(x => x.Password).NotEmpty().MinimumLength(12).MaximumLength(1024).OverridePropertyName("password");
        RuleFor(x => new UserRolesRequest(x.Roles)).SetValidator(new UserRolesValidator());
    }
}

public sealed class UserStatusValidator : AbstractValidator<UserStatusRequest>
{
    public UserStatusValidator() => RuleFor(x => x.IsActive).NotNull().OverridePropertyName("isActive");
}
