using CBL.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CBL.UnitTests.Support;

/// <summary>Fábrica de <see cref="CblDbContext"/> con EF InMemory; varios contextos pueden compartir el mismo almacén.</summary>
public static class TestDb
{
    public static string NewStoreName() => $"unit-{Guid.NewGuid():N}";

    public static CblDbContext Create(string? storeName = null)
    {
        var options = new DbContextOptionsBuilder<CblDbContext>()
            .UseInMemoryDatabase(storeName ?? NewStoreName())
            .Options;

        return new CblDbContext(options);
    }
}
