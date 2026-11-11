using CBL.Domain.Loans;
using CBL.Domain.Users;

namespace CBL.Application.Loans;

/// <summary>Préstamo tal como lo ve la API. Nunca se expone una entidad.</summary>
public sealed record LoanDto(
    Guid Id,
    Guid BorrowerId,
    string BorrowerName,
    string BorrowerEmail,
    decimal Amount,
    int TermMonths,
    string Status,
    DateTime CreatedAt,
    DateTime? DecidedAt,
    Guid? DecidedById,
    string? DecidedByName,
    int Version);

public sealed record LoanStatusResponse(Guid Id, string Status, DateTime? DecidedAt, int Version);

public static class LoanMapping
{
    /// <summary>Usa los usuarios indicados o, si no, las navegaciones cargadas por el repositorio.</summary>
    public static LoanDto ToDto(this Loan loan, User? borrower = null, User? decidedBy = null)
    {
        borrower ??= loan.Borrower;
        decidedBy ??= loan.DecidedBy;

        return new LoanDto(
            loan.Id,
            loan.BorrowerId,
            borrower?.FullName ?? string.Empty,
            borrower?.Email ?? string.Empty,
            loan.Amount,
            loan.TermMonths,
            loan.Status.ToString(),
            loan.CreatedAt,
            loan.DecidedAt,
            loan.DecidedByUserId,
            decidedBy?.FullName,
            loan.Version);
    }

    public static LoanStatusResponse ToStatusResponse(this Loan loan) =>
        new(loan.Id, loan.Status.ToString(), loan.DecidedAt, loan.Version);
}
