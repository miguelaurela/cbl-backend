namespace CBL.Application.Auth;

/// <summary>Bloqueo de inicio de sesión: por defecto 5 intentos fallidos durante 15 minutos (configurable).</summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    public int MaxFailedAttempts { get; set; } = 5;

    public int LockoutMinutes { get; set; } = 15;

    public void Validate()
    {
        if (MaxFailedAttempts < 1)
        {
            throw new InvalidOperationException("Auth:MaxFailedAttempts debe ser mayor que 0.");
        }

        if (LockoutMinutes < 1)
        {
            throw new InvalidOperationException("Auth:LockoutMinutes debe ser mayor que 0.");
        }
    }
}
