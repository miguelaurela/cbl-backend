using System.Globalization;
using System.Security.Claims;
using CBL.Application.Abstractions;
using CBL.Application.Common.Exceptions;
using CBL.Domain.Users;

namespace CBL.Api.Security;

/// <summary>
/// Usuario autenticado a partir de los claims del token (anti-IDOR: el id nunca viene del cuerpo ni de la ruta).
/// Vive en <c>CBL.Api</c> porque depende de HttpContext.
/// </summary>
internal sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid UserId =>
        Guid.TryParse(Principal?.FindFirstValue("sub"), out var id)
            ? id
            : throw new UnauthorizedException("No hay una sesión válida.");

    public Role Role =>
        Enum.TryParse<Role>(Principal?.FindFirstValue("role"), out var role)
            ? role
            : throw new UnauthorizedException("No hay una sesión válida.");

    public string? TokenId => Principal?.FindFirstValue("jti");

    public DateTime? TokenExpiresAtUtc =>
        long.TryParse(Principal?.FindFirstValue("exp"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var exp)
            ? DateTimeOffset.FromUnixTimeSeconds(exp).UtcDateTime
            : null;
}
