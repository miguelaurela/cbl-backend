using CBL.Domain.Loans;
using CBL.Domain.Users;

namespace CBL.UnitTests.Support;

/// <summary>Constructores de entidades de prueba.</summary>
public static class Builders
{
    public static readonly DateTime Now = new(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);

    public static User NewUser(Role role = Role.Borrower, string? email = null, bool active = true, Guid? id = null)
    {
        var user = User.Create(
            email ?? $"{Guid.NewGuid():N}@test.com",
            "Usuario de prueba",
            "hash-de-prueba",
            role,
            Now,
            id);

        if (!active)
        {
            user.Deactivate();
        }

        return user;
    }

    public static Loan PendingLoan(Guid? borrowerId = null, decimal amount = 5000m, int termMonths = 12) =>
        Loan.Request(borrowerId ?? Guid.NewGuid(), amount, termMonths, Now);
}
