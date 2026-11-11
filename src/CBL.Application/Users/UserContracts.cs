using CBL.Domain.Users;
using FluentValidation;

namespace CBL.Application.Users;

public sealed record UserDto(Guid Id, string Email, string FullName, string Role, bool IsActive, DateTime CreatedAt);

/// <summary>Alta de usuario por un Admin. La contraseña debe tener al menos 8 caracteres (la semilla "123" es solo de ejemplo).</summary>
public sealed record CreateUserRequest(string Email, string FullName, string Password, string Role);

/// <summary>
/// Actualización por un Admin; la desactivación es lógica (<c>isActive=false</c>). La contraseña es opcional (restablece).
/// </summary>
public sealed record UpdateUserRequest(string FullName, string Role, bool IsActive, string? Password = null);

public static class UserMapping
{
    public static UserDto ToDto(this User user) =>
        new(user.Id, user.Email, user.FullName, user.Role.ToString(), user.IsActive, user.CreatedAt);
}

internal static class UserRules
{
    public const int PasswordMinLength = 8;

    public static bool IsValidRole(string? role) =>
        Enum.TryParse<Role>(role, ignoreCase: false, out var parsed) && Enum.IsDefined(parsed);
}

public sealed class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("El correo es obligatorio.")
            .MaximumLength(User.EmailMaxLength).WithMessage($"El correo admite como máximo {User.EmailMaxLength} caracteres.")
            .EmailAddress().WithMessage("El correo no tiene un formato válido.");

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(User.FullNameMaxLength).WithMessage($"El nombre admite como máximo {User.FullNameMaxLength} caracteres.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("La contraseña es obligatoria.")
            .MinimumLength(UserRules.PasswordMinLength).WithMessage($"La contraseña debe tener al menos {UserRules.PasswordMinLength} caracteres.")
            .MaximumLength(200).WithMessage("La contraseña admite como máximo 200 caracteres.");

        RuleFor(x => x.Role)
            .Must(UserRules.IsValidRole).WithMessage("El rol debe ser Admin o Borrower.");
    }
}

public sealed class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(User.FullNameMaxLength).WithMessage($"El nombre admite como máximo {User.FullNameMaxLength} caracteres.");

        RuleFor(x => x.Role)
            .Must(UserRules.IsValidRole).WithMessage("El rol debe ser Admin o Borrower.");

        // La contraseña es opcional; si viene, cumple la misma política que al crear.
        When(x => !string.IsNullOrEmpty(x.Password), () =>
            RuleFor(x => x.Password!)
                .MinimumLength(UserRules.PasswordMinLength).WithMessage($"La contraseña debe tener al menos {UserRules.PasswordMinLength} caracteres.")
                .MaximumLength(200).WithMessage("La contraseña admite como máximo 200 caracteres.")
                .OverridePropertyName(nameof(UpdateUserRequest.Password)));
    }
}
