using CBL.Application.Abstractions;
using CBL.Application.Common.Exceptions;
using CBL.Domain.Loans;

namespace CBL.Application.Loans.Commands;

/// <summary>Cuerpo opcional de aprobar/rechazar: si trae <c>version</c> y no coincide con la actual, se responde 409.</summary>
public sealed record DecideLoanRequest(int? Version = null);

/// <summary>
/// Decide sobre un préstamo Pendiente (FR-014 a FR-017): cambia el estado, añade la fila de auditoría (quién y cuándo)
/// y guarda TODO en un único <c>SaveChangesAsync</c> (atómico). La caché solo se invalida después de confirmar el guardado.
/// Si dos administradores deciden a la vez, el token de versión hace que solo una prevalezca (409 para la otra).
/// </summary>
public sealed class DecideLoanHandler(
    ILoanRepository loans,
    IUserRepository users,
    ILoanAuditRepository audits,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IClock clock,
    IReadCache cache,
    ICorrelationIdAccessor correlation)
{
    public async Task<LoanDto> HandleAsync(Guid loanId, LoanStatus decision, int? expectedVersion, CancellationToken ct)
    {
        var loan = await loans.GetByIdAsync(loanId, ct) ?? throw new NotFoundException("El préstamo no existe.");

        if (expectedVersion is { } version && version != loan.Version)
        {
            throw new ConcurrencyConflictException(
                "El préstamo fue modificado por otra operación; vuelve a consultarlo e inténtalo de nuevo.");
        }

        var adminId = currentUser.UserId;
        var now = clock.UtcNow;
        var previousStatus = loan.Status;

        if (decision == LoanStatus.Approved)
        {
            loan.Approve(adminId, now);
        }
        else
        {
            loan.Reject(adminId, now);
        }

        await audits.AddAsync(
            LoanDecisionAudit.Create(loan.Id, adminId, decision, previousStatus, now, correlation.CorrelationId), ct);

        await unitOfWork.SaveChangesAsync(ct);

        // Solo tras confirmar el guardado: si falla, la caché sigue siendo coherente con la base.
        cache.Remove(CacheKeys.LoanStatus(loan.Id));

        var admin = await users.GetByIdAsync(adminId, ct);

        return loan.ToDto(decidedBy: admin);
    }
}

public sealed class ApproveLoanHandler(DecideLoanHandler decide)
{
    public Task<LoanDto> HandleAsync(Guid loanId, int? expectedVersion, CancellationToken ct) =>
        decide.HandleAsync(loanId, LoanStatus.Approved, expectedVersion, ct);
}

public sealed class RejectLoanHandler(DecideLoanHandler decide)
{
    public Task<LoanDto> HandleAsync(Guid loanId, int? expectedVersion, CancellationToken ct) =>
        decide.HandleAsync(loanId, LoanStatus.Rejected, expectedVersion, ct);
}
