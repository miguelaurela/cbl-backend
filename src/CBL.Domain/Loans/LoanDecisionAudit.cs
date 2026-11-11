namespace CBL.Domain.Loans;

/// <summary>Registro de solo inserción de una decisión: quién y cuándo (FR-016).</summary>
public sealed class LoanDecisionAudit
{
    public const int CorrelationIdMaxLength = 64;

    private LoanDecisionAudit()
    {
    }

    public Guid Id { get; private set; }

    public Guid LoanId { get; private set; }

    public Guid AdminUserId { get; private set; }

    public LoanStatus Decision { get; private set; }

    public LoanStatus PreviousStatus { get; private set; }

    public DateTime DecidedAt { get; private set; }

    public string? CorrelationId { get; private set; }

    public static LoanDecisionAudit Create(
        Guid loanId,
        Guid adminUserId,
        LoanStatus decision,
        LoanStatus previousStatus,
        DateTime decidedAtUtc,
        string? correlationId) =>
        new()
        {
            Id = Guid.NewGuid(),
            LoanId = loanId,
            AdminUserId = adminUserId,
            Decision = decision,
            PreviousStatus = previousStatus,
            DecidedAt = decidedAtUtc,
            CorrelationId = correlationId is { Length: > CorrelationIdMaxLength }
                ? correlationId[..CorrelationIdMaxLength]
                : correlationId
        };
}
