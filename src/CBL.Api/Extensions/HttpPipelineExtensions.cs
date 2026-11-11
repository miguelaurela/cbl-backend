using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace CBL.Api.Extensions;

public sealed class RateLimitingSettings
{
    public const string SectionName = "RateLimiting";

    /// <summary>Solicitudes por minuto y por IP en toda la API.</summary>
    public int GlobalPerMinute { get; set; } = 100;

    /// <summary>Solicitudes por minuto y por IP al inicio de sesión (más estricto). Debe superar Auth:MaxFailedAttempts.</summary>
    public int LoginPerMinute { get; set; } = 20;
}

public static class HttpPipelineExtensions
{
    public const string CorsPolicyName = "Frontend";

    public const string LoginRateLimitPolicy = "login";

    /// <summary>CORS restringido al/los origen(es) del frontend (<c>Cors:AllowedOrigins</c>); nunca AllowAnyOrigin ni credenciales.</summary>
    public static IServiceCollection AddCblCors(this IServiceCollection services)
    {
        services.AddCors();
        services.AddOptions<CorsOptions>().Configure<IConfiguration>((options, configuration) =>
        {
            var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

            options.AddPolicy(CorsPolicyName, policy => policy
                .WithOrigins(origins)
                .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS")
                .WithHeaders("Authorization", "Content-Type", "X-Correlation-Id")
                .WithExposedHeaders("X-Correlation-Id", "Retry-After"));
        });

        return services;
    }

    /// <summary>Límite de tasa global por IP y política más estricta para el login; 429 con Retry-After y ProblemDetails.</summary>
    public static IServiceCollection AddCblRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(_ => { });
        services.AddOptions<RateLimiterOptions>().Configure<IConfiguration>((options, configuration) =>
        {
            var settings = configuration.GetSection(RateLimitingSettings.SectionName).Get<RateLimitingSettings>()
                           ?? new RateLimitingSettings();

            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter(ClientKey(context), _ => Window(settings.GlobalPerMinute)));

            options.AddPolicy(LoginRateLimitPolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(ClientKey(context), _ => Window(settings.LoginPerMinute)));

            options.OnRejected = async (context, cancellationToken) =>
            {
                var http = context.HttpContext;

                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    http.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                }

                http.Response.StatusCode = StatusCodes.Status429TooManyRequests;

                var problems = http.RequestServices.GetRequiredService<IProblemDetailsService>();
                await problems.TryWriteAsync(new ProblemDetailsContext
                {
                    HttpContext = http,
                    ProblemDetails =
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Title = "Demasiadas solicitudes.",
                        Detail = "Se superó el límite de solicitudes. Inténtalo de nuevo en unos instantes."
                    }
                });
            };
        });

        return services;
    }

    private static string ClientKey(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private static FixedWindowRateLimiterOptions Window(int permitLimit) => new()
    {
        PermitLimit = permitLimit,
        Window = TimeSpan.FromMinutes(1),
        QueueLimit = 0,
        AutoReplenishment = true
    };
}
