using CBL.Application.Abstractions;
using CBL.Application.Parameters;
using CBL.Domain.Loans;

namespace CBL.Application.Loans.Commands;

/// <summary>Solicitar un préstamo (FR-006 a FR-010). El prestamista es siempre el del token (anti-IDOR).</summary>
public sealed class RequestLoanHandler(
    ILoanRepository loans,
    IUserRepository users,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IClock clock,
    LoanLimitsReader limitsReader)
{
    public async Task<LoanDto> HandleAsync(CreateLoanRequest request, CancellationToken ct)
    {
        await LoanRules.EnsureWithinLimitsAsync(limitsReader, request.Amount, ct);

        var borrowerId = currentUser.UserId;
        var loan = Loan.Request(borrowerId, request.Amount, request.TermMonths, clock.UtcNow);

        await loans.AddAsync(loan, ct);
        await unitOfWork.SaveChangesAsync(ct);

        var borrower = await users.GetByIdAsync(borrowerId, ct);

        return loan.ToDto(borrower);
    }
}
