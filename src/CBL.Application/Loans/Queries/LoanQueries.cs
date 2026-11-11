using CBL.Application.Abstractions;
using CBL.Application.Common.Exceptions;
using CBL.Domain.Loans;
using CBL.Domain.Users;

namespace CBL.Application.Loans.Queries;

internal static class LoanAccess
{
    /// <summary>El dueño o un Admin pueden ver un préstamo; para cualquier otro es "inexistente" (anti-enumeración).</summary>
    public static bool CanView(Guid borrowerId, ICurrentUser user) =>
        user.Role == Role.Admin || borrowerId == user.UserId;
}

/// <summary>Mis préstamos (solo los del prestamista autenticado; FR-011).</summary>
public sealed class GetMyLoansHandler(ILoanRepository loans, ICurrentUser currentUser)
{
    public async Task<IReadOnlyList<LoanDto>> HandleAsync(CancellationToken ct)
    {
        var mine = await loans.ListByBorrowerAsync(currentUser.UserId, ct);

        return mine.Select(l => l.ToDto()).ToList();
    }
}

/// <summary>Todos los préstamos para el Admin, con filtro opcional por estado y sin paginación (FR-013).</summary>
public sealed class GetAllLoansHandler(ILoanRepository loans)
{
    public async Task<IReadOnlyList<LoanDto>> HandleAsync(LoanStatus? status, CancellationToken ct)
    {
        var all = await loans.ListAsync(status, ct);

        return all.Select(l => l.ToDto()).ToList();
    }
}

public sealed class GetLoanByIdHandler(ILoanRepository loans, ICurrentUser currentUser)
{
    public async Task<LoanDto> HandleAsync(Guid id, CancellationToken ct)
    {
        var loan = await loans.GetByIdAsync(id, ct);

        if (loan is null || !LoanAccess.CanView(loan.BorrowerId, currentUser))
        {
            throw new NotFoundException("El préstamo no existe.");
        }

        return loan.ToDto();
    }
}

/// <summary>
/// Estado de un préstamo con lectura a través de caché (expiración absoluta de 5 minutos). La caché guarda el estado y
/// el dueño, pero la AUTORIZACIÓN se evalúa siempre antes de responder: el prestamista B nunca lee el estado cacheado
/// del prestamista A (FR-018, FR-019).
/// </summary>
public sealed class GetLoanStatusHandler(ILoanRepository loans, ICurrentUser currentUser, IReadCache cache)
{
    private sealed record CachedStatus(Guid BorrowerId, LoanStatusResponse Status);

    public async Task<LoanStatusResponse> HandleAsync(Guid id, CancellationToken ct)
    {
        var key = CacheKeys.LoanStatus(id);

        if (!cache.TryGet<CachedStatus>(key, out var cached) || cached is null)
        {
            var loan = await loans.GetByIdAsync(id, ct) ?? throw new NotFoundException("El préstamo no existe.");

            cached = new CachedStatus(loan.BorrowerId, loan.ToStatusResponse());
            cache.Set(key, cached);
        }

        if (!LoanAccess.CanView(cached.BorrowerId, currentUser))
        {
            throw new NotFoundException("El préstamo no existe.");
        }

        return cached.Status;
    }
}
