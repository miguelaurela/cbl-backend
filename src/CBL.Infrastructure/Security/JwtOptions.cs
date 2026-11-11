using System.Text;

namespace CBL.Infrastructure.Security;

/// <summary>
/// JWT de vida corta (10 minutos) sin refresh token. La clave de firma SOLO viene de variables de entorno
/// o user-secrets (<c>Jwt__Key</c>); nunca del repositorio.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public const int MinKeyBytes = 32;

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    public string Key { get; set; } = string.Empty;

    public int LifetimeMinutes { get; set; } = 10;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Issuer) || string.IsNullOrWhiteSpace(Audience))
        {
            throw new InvalidOperationException("Jwt:Issuer y Jwt:Audience son obligatorios.");
        }

        if (LifetimeMinutes < 1)
        {
            throw new InvalidOperationException("Jwt:LifetimeMinutes debe ser mayor que 0.");
        }

        if (Encoding.UTF8.GetByteCount(Key ?? string.Empty) < MinKeyBytes)
        {
            throw new InvalidOperationException(
                $"Falta la clave de firma Jwt:Key o es demasiado corta (mínimo {MinKeyBytes} bytes). " +
                "Defínala con la variable de entorno Jwt__Key o con user-secrets; nunca en el repositorio.");
        }
    }
}
