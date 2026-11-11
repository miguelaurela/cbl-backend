using System.Globalization;
using CBL.Application.Abstractions;
using CBL.Domain.Parameters;

namespace CBL.Application.Parameters;

/// <summary>Límites de monto vigentes; sirve a la vez de resultado del lector y de DTO de la API (<c>minAmount</c>/<c>maxAmount</c>).</summary>
public sealed record LoanLimits(decimal MinAmount, decimal MaxAmount);

/// <summary>
/// Lee los límites de <c>SystemParameters</c> a través de la caché de lectura (clave <c>loan-limits</c>, expiración
/// absoluta de 5 minutos). Quien los modifica elimina la clave después de guardar (FR-023, FR-024).
/// </summary>
public sealed class LoanLimitsReader(ISystemParameterRepository parameters, IReadCache cache)
{
    public async Task<LoanLimits> GetAsync(CancellationToken ct)
    {
        if (cache.TryGet<LoanLimits>(CacheKeys.LoanLimits, out var cached) && cached is not null)
        {
            return cached;
        }

        var min = await ReadAsync(SystemParameter.LoanAmountMin, ct);
        var max = await ReadAsync(SystemParameter.LoanAmountMax, ct);
        var limits = new LoanLimits(min, max);

        cache.Set(CacheKeys.LoanLimits, limits);

        return limits;
    }

    private async Task<decimal> ReadAsync(string key, CancellationToken ct)
    {
        var parameter = await parameters.GetAsync(key, ct)
                        ?? throw new InvalidOperationException($"Falta el parámetro de configuración '{key}'.");

        return decimal.Parse(parameter.Value, NumberStyles.Number, CultureInfo.InvariantCulture);
    }
}
