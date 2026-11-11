namespace CBL.Domain.Users;

public sealed class User
{
    public const int EmailMaxLength = 256;
    public const int FullNameMaxLength = 200;
    public const int PasswordHashMaxLength = 500;

    private User()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>Correo normalizado en minúsculas; único (no distingue mayúsculas).</summary>
    public string Email { get; private set; } = string.Empty;

    public string FullName { get; private set; } = string.Empty;

    public string PasswordHash { get; private set; } = string.Empty;

    public Role Role { get; private set; }

    /// <summary>La desactivación es lógica: un usuario con historial nunca se elimina.</summary>
    public bool IsActive { get; private set; } = true;

    public DateTime CreatedAt { get; private set; }

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public static User Create(
        string email,
        string fullName,
        string passwordHash,
        Role role,
        DateTime nowUtc,
        Guid? id = null) =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            Email = NormalizeEmail(email),
            FullName = fullName.Trim(),
            PasswordHash = passwordHash,
            Role = role,
            IsActive = true,
            CreatedAt = nowUtc
        };

    public void Update(string fullName, Role role)
    {
        FullName = fullName.Trim();
        Role = role;
    }

    public void SetPasswordHash(string passwordHash) => PasswordHash = passwordHash;

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}
