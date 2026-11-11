using CBL.Domain.Users;

namespace CBL.Application.Abstractions;

public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string passwordHash, string password);
}

/// <summary>Token de acceso emitido (JWT de vida corta, sin refresh token).</summary>
public sealed record IssuedToken(string AccessToken, DateTime ExpiresAtUtc, string TokenId);

public interface ITokenService
{
    IssuedToken Issue(User user);
}

/// <summary>
/// Contador de intentos de inicio de sesión fallidos por correo (exista o no el correo).
/// Vive en memoria del proceso: se pierde al reiniciar y no se comparte entre instancias.
/// </summary>
public interface ILoginAttemptTracker
{
    /// <summary>Indica si el correo está bloqueado y, en ese caso, cuánto falta para que termine el bloqueo.</summary>
    bool IsLocked(string normalizedEmail, out TimeSpan retryAfter);

    void RegisterFailure(string normalizedEmail);

    void Reset(string normalizedEmail);
}

/// <summary>Lista en memoria de tokens revocados por cierre de sesión; cada entrada caduca con el token.</summary>
public interface ITokenRevocationStore
{
    void Revoke(string tokenId, DateTime expiresAtUtc);

    bool IsRevoked(string tokenId);
}
