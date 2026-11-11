using CBL.Application.Abstractions;
using CBL.Application.Common.Exceptions;

namespace CBL.Application.Auth.Logout;

/// <summary>
/// Cierre de sesión (FR-004): revoca el <c>jti</c> del token actual hasta su expiración, de modo que cualquier uso
/// posterior responde 401 aunque no hayan pasado los 10 minutos de vida del token.
/// </summary>
public sealed class LogoutHandler(ICurrentUser currentUser, ITokenRevocationStore revocations, IClock clock)
{
    public void Handle()
    {
        var tokenId = currentUser.TokenId ?? throw new UnauthorizedException("No hay una sesión válida.");

        // Sin claim exp (no debería ocurrir con nuestros tokens) se retiene el máximo razonable.
        var expiresAt = currentUser.TokenExpiresAtUtc ?? clock.UtcNow.AddMinutes(15);

        revocations.Revoke(tokenId, expiresAt);
    }
}
