using CBL.Application.Abstractions;
using CBL.Application.Common.Exceptions;
using CBL.Application.Parameters;
using CBL.Domain.Users;

namespace CBL.Application.Loans.Commands;

/// <summary>Un Borrower edita SOLO su propio préstamo y solo mientras esté Pendiente (FR-012).</summary>
public sealed class UpdateLoanHandler(
    ILoanRepository loans,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IReadCache cache,
    LoanLimitsReader limitsReader)
{
    public async Task<LoanDto> HandleAsync(Guid id, CreateLoanRequest request, CancellationToken ct)
    {
        var loan = await loans.GetByIdAsync(id, ct);

        // El préstamo de otro prestamista se trata como inexistente (no se permite enumerar).
        if (loan is null || loan.BorrowerId != currentUser.UserId)
        {
            throw new NotFoundException("El préstamo no existe.");
        }

        loan.EnsureCanModify();
        await LoanRules.EnsureWithinLimitsAsync(limitsReader, request.Amount, ct);

        loan.UpdateTerms(request.Amount, request.TermMonths);
        await unitOfWork.SaveChangesAsync(ct);

        cache.Remove(CacheKeys.LoanStatus(loan.Id));

        return loan.ToDto();
    }
}

/// <summary>El dueño elimina su préstamo Pendiente; un Admin elimina solo préstamos Pendientes (FR-012).</summary>
public sealed class DeleteLoanHandler(
    ILoanRepository loans,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IReadCache cache)
{
    public async Task HandleAsync(Guid id, CancellationToken ct)
    {
        var loan = await loans.GetByIdAsync(id, ct);

        if (loan is null || (currentUser.Role != Role.Admin && loan.BorrowerId != currentUser.UserId))
        {
            throw new NotFoundException("El préstamo no existe.");
        }

        loan.EnsureCanDelete();

        loans.Remove(loan);
        await unitOfWork.SaveChangesAsync(ct);

        cache.Remove(CacheKeys.LoanStatus(loan.Id));
    }
}
