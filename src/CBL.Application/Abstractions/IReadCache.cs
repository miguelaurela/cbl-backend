namespace CBL.Application.Abstractions;

/// <summary>
/// Caché de lectura (solo consultas de estado de préstamos y límites). Expiración absoluta de 5 minutos;
/// la invalidación es explícita y ocurre después de confirmar el guardado (constitución, principio V).
/// </summary>
public interface IReadCache
{
    bool TryGet<T>(string key, out T? value);

    void Set<T>(string key, T value);

    void Remove(string key);
}

public static class CacheKeys
{
    public const string LoanLimits = "loan-limits";

    public static string LoanStatus(Guid loanId) => $"loan-status:{loanId}";
}
