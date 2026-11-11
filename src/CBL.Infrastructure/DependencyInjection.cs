using CBL.Application.Abstractions;
using CBL.Application.Auth;
using CBL.Infrastructure.Caching;
using CBL.Infrastructure.Persistence;
using CBL.Infrastructure.Persistence.Repositories;
using CBL.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CBL.Infrastructure;

public static class DependencyInjection
{
    public const string InMemoryProvider = "InMemory";

    public const string SqlServerProvider = "SqlServer";

    /// <summary>
    /// Elige el proveedor: <c>Database:Provider</c> manda; si está vacío, InMemory en Development y SqlServer en el resto.
    /// </summary>
    public static string ResolveProvider(IConfiguration configuration, bool isDevelopment)
    {
        var configured = configuration["Database:Provider"];

        if (string.IsNullOrWhiteSpace(configured))
        {
            return isDevelopment ? InMemoryProvider : SqlServerProvider;
        }

        return configured.Equals(InMemoryProvider, StringComparison.OrdinalIgnoreCase) ? InMemoryProvider
            : configured.Equals(SqlServerProvider, StringComparison.OrdinalIgnoreCase) ? SqlServerProvider
            : throw new InvalidOperationException(
                $"Database:Provider '{configured}' no es válido. Use '{InMemoryProvider}' o '{SqlServerProvider}'.");
    }

    /// <summary>
    /// Registra los adaptadores. La configuración se lee de forma diferida (al construir cada servicio) para
    /// que los valores definidos por el host de pruebas o por variables de entorno se respeten siempre.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, bool isDevelopment)
    {
        var inMemoryName = $"cbl-{Guid.NewGuid():N}";

        services.AddDbContext<CblDbContext>((sp, options) =>
        {
            var configuration = sp.GetRequiredService<IConfiguration>();

            if (ResolveProvider(configuration, isDevelopment) == InMemoryProvider)
            {
                options.UseInMemoryDatabase(inMemoryName);
                return;
            }

            var connectionString = configuration.GetConnectionString("Cbl");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "Falta la cadena de conexión 'ConnectionStrings:Cbl' (variable ConnectionStrings__Cbl o user-secrets).");
            }

            options.UseSqlServer(connectionString);
        });

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ILoanRepository, LoanRepository>();
        services.AddScoped<ISystemParameterRepository, SystemParameterRepository>();
        services.AddScoped<ILoanAuditRepository, LoanAuditRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IReadCache, MemoryReadCache>();

        services.AddSingleton<IPasswordHasher, PasswordHasherAdapter>();
        services.AddSingleton<ITokenService>(sp =>
            new JwtTokenService(sp.GetRequiredService<JwtOptions>(), sp.GetRequiredService<IClock>()));
        services.AddSingleton<ILoginAttemptTracker>(sp =>
            new LoginAttemptTracker(sp.GetRequiredService<AuthOptions>(), sp.GetRequiredService<IClock>()));

        // Opciones validadas al crearse; Program.cs las resuelve al arrancar para fallar rápido si algo falta.
        services.AddSingleton(sp =>
        {
            var jwt = sp.GetRequiredService<IConfiguration>().GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
            jwt.Validate();
            return jwt;
        });

        services.AddSingleton(sp =>
        {
            var auth = sp.GetRequiredService<IConfiguration>().GetSection(AuthOptions.SectionName).Get<AuthOptions>() ?? new AuthOptions();
            auth.Validate();
            return auth;
        });

        return services;
    }
}
