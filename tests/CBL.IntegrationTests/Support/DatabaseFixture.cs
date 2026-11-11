using CBL.Application.Abstractions;
using CBL.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace CBL.IntegrationTests.Support;

/// <summary>Una sola API y una sola base para toda la colección; cada prueba la restablece al estado inicial.</summary>
public sealed class DatabaseFixture : IDisposable
{
    public CblWebApplicationFactory Factory { get; } = new();

    public async Task ResetAsync()
    {
        if (!string.IsNullOrWhiteSpace(TestEnvironment.SqlConnectionString))
        {
            await ResetSqlServerAsync(TestEnvironment.SqlConnectionString);
            return;
        }

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CblDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
        await InMemorySeeder.SeedAsync(db, scope.ServiceProvider.GetRequiredService<IClock>());
    }

    /// <summary>Borra todas las filas (respetando las claves foráneas) y vuelve a aplicar el seed del .sqlproj.</summary>
    private static async Task ResetSqlServerAsync(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        const string clear = """
            DELETE FROM [dbo].[LoanDecisionAudit];
            DELETE FROM [dbo].[Loans];
            DELETE FROM [dbo].[SystemParameters];
            DELETE FROM [dbo].[Users];
            """;

        await using (var command = new SqlCommand(clear, connection))
        {
            await command.ExecuteNonQueryAsync();
        }

        await using var seed = new SqlCommand(await File.ReadAllTextAsync(SeedScriptPath()), connection);
        await seed.ExecuteNonQueryAsync();
    }

    private static string SeedScriptPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CBL.sln")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(
            directory?.FullName ?? throw new InvalidOperationException("No se encontró CBL.sln."),
            "db", "CBL.Database", "PostDeployment", "Seed.sql");
    }

    public void Dispose() => Factory.Dispose();
}

[CollectionDefinition(Name)]
public sealed class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
    public const string Name = "Database";
}
