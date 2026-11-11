using CBL.Application.Abstractions;
using CBL.Domain.Parameters;
using CBL.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace CBL.Infrastructure.Persistence;

/// <summary>
/// Datos iniciales. DEBEN coincidir con db/CBL.Database/PostDeployment/Seed.sql.
/// La contraseña "123" es SOLO DE EJEMPLO para la demostración.
/// </summary>
public static class SeedData
{
    public static readonly Guid BorrowerId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static readonly Guid AdminId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public const string BorrowerEmail = "usuario@test.com";

    public const string AdminEmail = "admin@test.com";

    public const string SamplePassword = "123";

    /// <summary>Hash PBKDF2 (PasswordHasher) de la contraseña de ejemplo; es el mismo que usa Seed.sql.</summary>
    public const string SamplePasswordHash =
        "AQAAAAIAAYagAAAAEOjxHxoKoZxdhvVk1UpE1G86OJynfb4s1pbyFOPcDKNj7wWtu8/rcOWY+SuLOjxDIA==";

    public const decimal DefaultLoanAmountMin = 1000m;

    public const decimal DefaultLoanAmountMax = 100000m;
}

/// <summary>Inserta los datos iniciales en una base EF InMemory (ejecución local y pruebas).</summary>
public static class InMemorySeeder
{
    public static async Task SeedAsync(CblDbContext db, IClock clock, CancellationToken ct = default)
    {
        var now = clock.UtcNow;

        if (!await db.Users.AnyAsync(ct))
        {
            await db.Users.AddRangeAsync(
                User.Create(SeedData.BorrowerEmail, "Usuario de prueba", SeedData.SamplePasswordHash, Role.Borrower, now, SeedData.BorrowerId),
                User.Create(SeedData.AdminEmail, "Administrador de prueba", SeedData.SamplePasswordHash, Role.Admin, now, SeedData.AdminId));
        }

        if (!await db.SystemParameters.AnyAsync(ct))
        {
            await db.SystemParameters.AddRangeAsync(
                SystemParameter.Create(
                    SystemParameter.LoanAmountMin,
                    SeedData.DefaultLoanAmountMin.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    now,
                    null),
                SystemParameter.Create(
                    SystemParameter.LoanAmountMax,
                    SeedData.DefaultLoanAmountMax.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    now,
                    null));
        }

        await db.SaveChangesAsync(ct);
    }
}
