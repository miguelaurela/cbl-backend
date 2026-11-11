using CBL.Application.Abstractions;

namespace CBL.Infrastructure.Security;

/// <summary>
/// Lista en memoria de tokens revocados por cierre de sesión. Cada entrada caduca junto con el token (más la tolerancia
/// de reloj con la que JwtBearer sigue aceptándolo), así que el almacén no crece sin límite. Se pierde al reiniciar el
/// proceso y no se comparte entre instancias: aceptable con una instancia y tokens de 10 minutos.
/// </summary>
public sealed class TokenRevocationStore(IClock clock) : ITokenRevocationStore
{
    /// <summary>JwtBearer acepta un token hasta <c>exp + ClockSkew</c> (30 s); la entrada debe vivir al menos ese tiempo.</summary>
    public static readonly TimeSpan Retention = TimeSpan.FromSeconds(30) + TimeSpan.FromMinutes(1);

    private readonly Dictionary<string, DateTime> _revoked = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _revoked.Count;
            }
        }
    }

    public void Revoke(string tokenId, DateTime expiresAtUtc)
    {
        lock (_gate)
        {
            var now = clock.UtcNow;

            foreach (var expired in _revoked.Where(e => e.Value <= now).Select(e => e.Key).ToList())
            {
                _revoked.Remove(expired);
            }

            _revoked[tokenId] = expiresAtUtc + Retention;
        }
    }

    public bool IsRevoked(string tokenId)
    {
        lock (_gate)
        {
            if (!_revoked.TryGetValue(tokenId, out var keepUntil))
            {
                return false;
            }

            if (keepUntil <= clock.UtcNow)
            {
                _revoked.Remove(tokenId);

                return false;
            }

            return true;
        }
    }
}
