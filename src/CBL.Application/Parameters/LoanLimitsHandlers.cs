using System.Globalization;
using CBL.Application.Abstractions;
using CBL.Domain.Parameters;
using FluentValidation;

namespace CBL.Application.Parameters;

/// <summary>Mínimo mayor que 0 y máximo mayor que el mínimo, ambos con 2 decimales como máximo (FR-024).</summary>
public sealed class LoanLimitsValidator : AbstractValidator<LoanLimits>
{
    /// <summary>Tope razonable que además cabe en decimal(18,2).</summary>
    public const decimal Ceiling = 1_000_000_000_000m;

    public LoanLimitsValidator()
    {
        RuleFor(x => x.MinAmount)
            .GreaterThan(0).WithMessage("El monto mínimo debe ser mayor que 0.")
            .LessThanOrEqualTo(Ceiling).WithMessage($"El monto mínimo no puede superar {Ceiling.ToString("N0", CultureInfo.InvariantCulture)}.")
            .Must(HaveAtMostTwoDecimals).WithMessage("El monto mínimo admite como máximo 2 decimales.");

        RuleFor(x => x.MaxAmount)
            .GreaterThan(x => x.MinAmount).WithMessage("El monto máximo debe ser mayor que el mínimo.")
            .LessThanOrEqualTo(Ceiling).WithMessage($"El monto máximo no puede superar {Ceiling.ToString("N0", CultureInfo.InvariantCulture)}.")
            .Must(HaveAtMostTwoDecimals).WithMessage("El monto máximo admite como máximo 2 decimales.");
    }

    private static bool HaveAtMostTwoDecimals(decimal value) => decimal.Round(value, 2) == value;
}

/// <summary>Límites vigentes; lo puede consultar cualquier usuario autenticado (el formulario del prestamista los usa).</summary>
public sealed class GetLoanLimitsHandler(LoanLimitsReader reader)
{
    public Task<LoanLimits> HandleAsync(CancellationToken ct) => reader.GetAsync(ct);
}

/// <summary>
/// Modifica los límites (solo Admin). El cambio se aplica a las solicitudes NUEVAS sin redesplegar; las ya registradas
/// conservan su monto. La caché de límites se invalida DESPUÉS de confirmar el guardado (FR-023, FR-024).
/// </summary>
public sealed class UpdateLoanLimitsHandler(
    ISystemParameterRepository parameters,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IClock clock,
    IReadCache cache)
{
    public async Task<LoanLimits> HandleAsync(LoanLimits request, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var adminId = currentUser.UserId;

        await UpsertAsync(SystemParameter.LoanAmountMin, request.MinAmount, now, adminId, ct);
        await UpsertAsync(SystemParameter.LoanAmountMax, request.MaxAmount, now, adminId, ct);

        await unitOfWork.SaveChangesAsync(ct);

        cache.Remove(CacheKeys.LoanLimits);

        return new LoanLimits(request.MinAmount, request.MaxAmount);
    }

    private async Task UpsertAsync(string key, decimal amount, DateTime now, Guid adminId, CancellationToken ct)
    {
        var value = amount.ToString(CultureInfo.InvariantCulture);
        var parameter = await parameters.GetAsync(key, ct);

        if (parameter is null)
        {
            await parameters.AddAsync(SystemParameter.Create(key, value, now, adminId), ct);
        }
        else
        {
            parameter.Update(value, now, adminId);
        }
    }
}
