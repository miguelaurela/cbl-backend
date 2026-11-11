using CBL.Domain.Loans;
using CBL.Domain.Parameters;
using CBL.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CBL.Infrastructure.Persistence;

/// <summary>
/// EF Core solo MAPEA el esquema definido en db/CBL.Database (fuente de verdad); no se usan migraciones.
/// Cada configuración replica de forma explícita tipos, longitudes, índices, claves y el token de versión.
/// </summary>
public sealed class CblDbContext(DbContextOptions<CblDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Loan> Loans => Set<Loan>();

    public DbSet<LoanDecisionAudit> LoanDecisionAudits => Set<LoanDecisionAudit>();

    public DbSet<SystemParameter> SystemParameters => Set<SystemParameter>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CblDbContext).Assembly);

    /// <summary>
    /// SQL Server devuelve <c>datetime2</c> con Kind Unspecified; se marcan como UTC para que la API serialice fechas
    /// ISO 8601 con "Z" (el contrato exige fechas UTC).
    /// </summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<NullableUtcDateTimeConverter>();
    }

    private sealed class UtcDateTimeConverter()
        : ValueConverter<DateTime, DateTime>(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

    private sealed class NullableUtcDateTimeConverter()
        : ValueConverter<DateTime?, DateTime?>(
            v => v,
            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);
}
