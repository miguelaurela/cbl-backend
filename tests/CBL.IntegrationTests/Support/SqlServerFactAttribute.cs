namespace CBL.IntegrationTests.Support;

/// <summary>
/// Pruebas de integración: SOLO en CI contra SQL Server real (variable <c>CBL_TEST_SQL</c> con la cadena de conexión
/// del esquema ya publicado desde el .dacpac). Sin esa variable se OMITEN (no fallan), así que un
/// <c>dotnet test</c> local sin Docker queda en verde (ADR-0021).
///
/// Atajo opcional solo para quien desarrolla: <c>CBL_TEST_INMEMORY=1</c> ejecuta las pruebas HTTP contra EF InMemory
/// (salvo las marcadas <see cref="RequiresSqlServer"/>), útil para validar el comportamiento sin SQL Server.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class SqlServerFactAttribute : FactAttribute
{
    public const string SqlEnvironmentVariable = "CBL_TEST_SQL";

    public const string InMemoryEnvironmentVariable = "CBL_TEST_INMEMORY";

    /// <summary>La prueba depende del motor real (transacciones, restricciones, esquema) y no corre con InMemory.</summary>
    public bool RequiresSqlServer { get; set; }

    public override string? Skip
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(TestEnvironment.SqlConnectionString))
            {
                return null;
            }

            if (!RequiresSqlServer && TestEnvironment.UseInMemory)
            {
                return null;
            }

            return $"Prueba de integración: defina {SqlEnvironmentVariable} (solo CI) para ejecutarla.";
        }
        set { }
    }
}

public static class TestEnvironment
{
    public static string? SqlConnectionString =>
        Environment.GetEnvironmentVariable(SqlServerFactAttribute.SqlEnvironmentVariable);

    public static bool UseInMemory =>
        Environment.GetEnvironmentVariable(SqlServerFactAttribute.InMemoryEnvironmentVariable) == "1";
}
