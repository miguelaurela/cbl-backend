using CBL.Domain.Loans;
using CBL.Domain.Parameters;
using CBL.Domain.Users;

namespace CBL.Application.Abstractions;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct);

    /// <summary>Busca por correo ya normalizado en minúsculas.</summary>
    Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken ct);

    Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken ct);

    Task<IReadOnlyList<User>> ListAsync(CancellationToken ct);

    Task AddAsync(User user, CancellationToken ct);
}

public interface ILoanRepository
{
    /// <summary>Devuelve el préstamo con prestamista y decisor cargados.</summary>
    Task<Loan?> GetByIdAsync(Guid id, CancellationToken ct);

    Task<IReadOnlyList<Loan>> ListByBorrowerAsync(Guid borrowerId, CancellationToken ct);

    /// <summary>Todos los préstamos, más recientes primero, con filtro opcional por estado y sin paginación.</summary>
    Task<IReadOnlyList<Loan>> ListAsync(LoanStatus? status, CancellationToken ct);

    Task AddAsync(Loan loan, CancellationToken ct);

    void Remove(Loan loan);
}

public interface ISystemParameterRepository
{
    Task<SystemParameter?> GetAsync(string key, CancellationToken ct);

    Task AddAsync(SystemParameter parameter, CancellationToken ct);
}

public interface ILoanAuditRepository
{
    Task AddAsync(LoanDecisionAudit audit, CancellationToken ct);
}

public interface IUnitOfWork
{
    /// <summary>
    /// Guarda todos los cambios pendientes en un único guardado atómico.
    /// Un conflicto de versión se traduce a <c>ConcurrencyConflictException</c>.
    /// </summary>
    Task SaveChangesAsync(CancellationToken ct);
}

/// <summary>Correlation ID de la petición actual (para el registro de auditoría).</summary>
public interface ICorrelationIdAccessor
{
    string? CorrelationId { get; }
}
