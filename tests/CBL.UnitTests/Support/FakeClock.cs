using CBL.Application.Abstractions;

namespace CBL.UnitTests.Support;

/// <summary>Reloj controlable para pruebas deterministas.</summary>
public sealed class FakeClock : IClock
{
    public DateTime UtcNow { get; set; } = new(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);

    public void Advance(TimeSpan by) => UtcNow += by;
}
