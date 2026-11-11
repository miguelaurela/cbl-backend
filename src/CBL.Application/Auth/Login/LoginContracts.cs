using FluentValidation;

namespace CBL.Application.Auth.Login;

public sealed record LoginRequest(string Email, string Password);

public sealed record LoginUserDto(Guid Id, string Email, string FullName, string Role);

public sealed record LoginResponse(string AccessToken, string TokenType, DateTime ExpiresAt, LoginUserDto User);

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("El correo es obligatorio.")
            .MaximumLength(256).WithMessage("El correo admite como máximo 256 caracteres.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("La contraseña es obligatoria.")
            .MaximumLength(200).WithMessage("La contraseña admite como máximo 200 caracteres.");
    }
}
