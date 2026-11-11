using CBL.Application.Abstractions;
using CBL.Domain.Users;
using Microsoft.AspNetCore.Identity;

namespace CBL.Infrastructure.Security;

/// <summary>PBKDF2 mediante <see cref="PasswordHasher{TUser}"/> (comparación segura incluida).</summary>
public sealed class PasswordHasherAdapter : IPasswordHasher
{
    private readonly PasswordHasher<User> _hasher = new();

    public string Hash(string password) => _hasher.HashPassword(default!, password);

    public bool Verify(string passwordHash, string password) =>
        _hasher.VerifyHashedPassword(default!, passwordHash, password)
            is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
}
