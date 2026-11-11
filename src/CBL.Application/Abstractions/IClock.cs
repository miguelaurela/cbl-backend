namespace CBL.Application.Abstractions;

public interface IClock
{
    /// <summary>Instante actual en UTC.</summary>
    DateTime UtcNow { get; }
}
