namespace CBL.Domain.Parameters;

/// <summary>Parámetro de configuración persistente (clave-valor), por ejemplo los límites de monto.</summary>
public sealed class SystemParameter
{
    public const string LoanAmountMin = "LoanAmountMin";
    public const string LoanAmountMax = "LoanAmountMax";

    private SystemParameter()
    {
    }

    public string Key { get; private set; } = string.Empty;

    public string Value { get; private set; } = string.Empty;

    public DateTime UpdatedAt { get; private set; }

    public Guid? UpdatedByUserId { get; private set; }

    public static SystemParameter Create(string key, string value, DateTime nowUtc, Guid? updatedByUserId) =>
        new() { Key = key, Value = value, UpdatedAt = nowUtc, UpdatedByUserId = updatedByUserId };

    public void Update(string value, DateTime nowUtc, Guid? updatedByUserId)
    {
        Value = value;
        UpdatedAt = nowUtc;
        UpdatedByUserId = updatedByUserId;
    }
}
