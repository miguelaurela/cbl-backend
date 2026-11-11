using CBL.Domain.Users;

namespace CBL.Application.Abstractions;

/// <summary>
/// Usuario autenticado de la petición actual. El id sale SIEMPRE del token, nunca del cuerpo ni de la ruta.
/// </summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    /// <summary>Id del usuario (claim <c>sub</c>). Lanza <c>UnauthorizedException</c> si no hay sesión.</summary>
    Guid UserId { get; }

    /// <summary>Rol del usuario (claim <c>role</c>). Lanza <c>UnauthorizedException</c> si no hay sesión.</summary>
    Role Role { get; }

    /// <summary>Identificador del token (claim <c>jti</c>).</summary>
    string? TokenId { get; }

    /// <summary>Expiración del token en UTC (claim <c>exp</c>).</summary>
    DateTime? TokenExpiresAtUtc { get; }
}
