using CBL.Application.Abstractions;

namespace CBL.Infrastructure;

internal sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
