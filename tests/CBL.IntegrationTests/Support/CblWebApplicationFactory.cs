using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CBL.IntegrationTests.Support;

/// <summary>
/// Arranca la API real en memoria (TestServer). Con <c>CBL_TEST_SQL</c> usa el SQL Server del contenedor de servicio
/// de CI (esquema desplegado desde el .dacpac); con <c>CBL_TEST_INMEMORY=1</c>, EF InMemory (atajo de desarrollo).
/// </summary>
public sealed class CblWebApplicationFactory(
    IDictionary<string, string?>? overrides = null,
    string environment = "Development",
    ILoggerProvider? loggerProvider = null) : WebApplicationFactory<Program>
{
    public const string TestJwtKey = "clave-solo-para-pruebas-0123456789-ABCDEFGHIJ";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);

        if (loggerProvider is not null)
        {
            builder.ConfigureLogging(logging => logging.AddProvider(loggerProvider));
        }

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            var settings = new Dictionary<string, string?>
            {
                ["Jwt:Key"] = TestJwtKey,
                // Límites altos por defecto: las pruebas comparten IP y no deben chocar con el rate limiting;
                // las pruebas de 429 los bajan con una fábrica propia.
                ["RateLimiting:GlobalPerMinute"] = "100000",
                ["RateLimiting:LoginPerMinute"] = "1000"
            };

            if (!string.IsNullOrWhiteSpace(TestEnvironment.SqlConnectionString))
            {
                settings["Database:Provider"] = "SqlServer";
                settings["ConnectionStrings:Cbl"] = TestEnvironment.SqlConnectionString;
            }
            else
            {
                settings["Database:Provider"] = "InMemory";
            }

            if (overrides is not null)
            {
                foreach (var (key, value) in overrides)
                {
                    settings[key] = value;
                }
            }

            configuration.AddInMemoryCollection(settings);
        });
    }
}
