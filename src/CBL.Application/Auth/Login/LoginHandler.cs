using CBL.Application.Abstractions;
using CBL.Application.Common.Exceptions;
using CBL.Domain.Users;

namespace CBL.Application.Auth.Login;

/// <summary>
/// Inicio de sesión. Correo inexistente, contraseña incorrecta y usuario desactivado devuelven el MISMO error
/// genérico; tras 5 fallos con un mismo correo (exista o no) se bloquea ese correo 15 minutos (FR-001 a FR-003).
/// </summary>
public sealed class LoginHandler(
    IUserRepository users,
    IPasswordHasher hasher,
    ITokenService tokens,
    ILoginAttemptTracker attempts)
{
    private const string InvalidCredentials = "Correo o contraseña incorrectos.";

    public async Task<LoginResponse> HandleAsync(LoginRequest request, CancellationToken ct)
    {
        var email = User.NormalizeEmail(request.Email);

        if (attempts.IsLocked(email, out var retryAfter))
        {
            throw new AccountLockedException(retryAfter);
        }

        var user = await users.GetByEmailAsync(email, ct);

        bool valid;
        if (user is null)
        {
            // Mismo coste de hash que un correo existente: evita enumerar correos por diferencia de tiempos.
            hasher.Hash(request.Password);
            valid = false;
        }
        else
        {
            valid = hasher.Verify(user.PasswordHash, request.Password) && user.IsActive;
        }

        if (!valid)
        {
            attempts.RegisterFailure(email);
            throw new UnauthorizedException(InvalidCredentials);
        }

        attempts.Reset(email);

        var token = tokens.Issue(user!);

        return new LoginResponse(
            token.AccessToken,
            "Bearer",
            token.ExpiresAtUtc,
            new LoginUserDto(user!.Id, user.Email, user.FullName, user.Role.ToString()));
    }
}
