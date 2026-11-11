using CBL.Domain.Exceptions;
using CBL.Domain.Users;

namespace CBL.Domain.Loans;

public sealed class Loan
{
    public const int MinTermMonths = 1;
    public const int MaxTermMonths = 60;

    private Loan()
    {
    }

    public Guid Id { get; private set; }

    public Guid BorrowerId { get; private set; }

    public User Borrower { get; private set; } = null!;

    /// <summary>Monto con 2 decimales como máximo (decimal(18,2)).</summary>
    public decimal Amount { get; private set; }

    public int TermMonths { get; private set; }

    public LoanStatus Status { get; private set; } = LoanStatus.Pending;

    public DateTime CreatedAt { get; private set; }

    public Guid? DecidedByUserId { get; private set; }

    public User? DecidedBy { get; private set; }

    public DateTime? DecidedAt { get; private set; }

    /// <summary>Token de concurrencia optimista; la propia entidad lo incrementa en cada cambio.</summary>
    public int Version { get; private set; } = 1;

    public static Loan Request(Guid borrowerId, decimal amount, int termMonths, DateTime nowUtc)
    {
        Validate(amount, termMonths);

        return new Loan
        {
            Id = Guid.NewGuid(),
            BorrowerId = borrowerId,
            Amount = amount,
            TermMonths = termMonths,
            Status = LoanStatus.Pending,
            CreatedAt = nowUtc,
            Version = 1
        };
    }

    public void Approve(Guid adminId, DateTime nowUtc) => Decide(LoanStatus.Approved, adminId, nowUtc);

    public void Reject(Guid adminId, DateTime nowUtc) => Decide(LoanStatus.Rejected, adminId, nowUtc);

    /// <summary>Edita monto y plazo; solo mientras el préstamo siga Pendiente.</summary>
    public void UpdateTerms(decimal amount, int termMonths)
    {
        EnsureCanModify();
        Validate(amount, termMonths);
        Amount = amount;
        TermMonths = termMonths;
        Version++;
    }

    /// <summary>Comprueba que el préstamo se puede eliminar (solo Pendiente).</summary>
    public void EnsureCanDelete() => EnsureCanModify();

    private void Decide(LoanStatus decision, Guid adminId, DateTime nowUtc)
    {
        if (Status != LoanStatus.Pending)
        {
            throw new InvalidLoanTransitionException(
                $"El préstamo ya fue decidido ({Status}); una decisión es final y no se puede revertir.");
        }

        Status = decision;
        DecidedByUserId = adminId;
        DecidedAt = nowUtc;
        Version++;
    }

    private void EnsureCanModify()
    {
        if (Status != LoanStatus.Pending)
        {
            throw new LoanNotModifiableException(
                $"El préstamo está {Status} y ya no se puede editar ni eliminar.");
        }
    }

    private static void Validate(decimal amount, int termMonths)
    {
        if (amount <= 0)
        {
            throw new DomainValidationException("amount", "El monto debe ser mayor que 0.");
        }

        if (decimal.Round(amount, 2) != amount)
        {
            throw new DomainValidationException("amount", "El monto admite como máximo 2 decimales.");
        }

        if (termMonths is < MinTermMonths or > MaxTermMonths)
        {
            throw new DomainValidationException(
                "termMonths", $"El plazo debe ser un entero entre {MinTermMonths} y {MaxTermMonths} meses.");
        }
    }
}
