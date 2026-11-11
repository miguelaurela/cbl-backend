using CBL.Application.Common.Exceptions;
using CBL.Application.Parameters;
using CBL.Domain.Loans;
using FluentValidation;

namespace CBL.Application.Loans;

/// <summary>Solicitud (y edición) de un préstamo. El prestamista NUNCA viaja en el cuerpo: sale del token.</summary>
public sealed record CreateLoanRequest(decimal Amount, int TermMonths);

public sealed class CreateLoanRequestValidator : AbstractValidator<CreateLoanRequest>
{
    public CreateLoanRequestValidator()
    {
        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("El monto debe ser mayor que 0.")
            .Must(amount => decimal.Round(amount, 2) == amount).WithMessage("El monto admite como máximo 2 decimales.");

        RuleFor(x => x.TermMonths)
            .InclusiveBetween(Loan.MinTermMonths, Loan.MaxTermMonths)
            .WithMessage($"El plazo debe ser un entero entre {Loan.MinTermMonths} y {Loan.MaxTermMonths} meses.");
    }
}

internal static class LoanRules
{
    /// <summary>El monto debe estar dentro de los límites vigentes (mínimo y máximo incluidos).</summary>
    public static async Task EnsureWithinLimitsAsync(LoanLimitsReader reader, decimal amount, CancellationToken ct)
    {
        var limits = await reader.GetAsync(ct);

        if (amount < limits.MinAmount || amount > limits.MaxAmount)
        {
            throw new ValidationFailedException(
                "amount",
                $"El monto debe estar entre {limits.MinAmount.ToString("N2", System.Globalization.CultureInfo.InvariantCulture)} " +
                $"y {limits.MaxAmount.ToString("N2", System.Globalization.CultureInfo.InvariantCulture)}.");
        }
    }
}
