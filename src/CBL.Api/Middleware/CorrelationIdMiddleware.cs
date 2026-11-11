using System.Diagnostics;
using System.Text.RegularExpressions;
using CBL.Application.Abstractions;

namespace CBL.Api.Middleware;

/// <summary>
/// Correlation ID (<c>X-Correlation-Id</c>) de entrada y salida; se usa como <c>traceId</c> de los errores y en el
/// ámbito de los logs. El registro de peticiones NUNCA incluye cuerpos, contraseñas, tokens ni cadenas de consulta.
/// </summary>
public sealed partial class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var incoming = context.Request.Headers[HeaderName].ToString();
        var correlationId = ValidId().IsMatch(incoming) ? incoming : Guid.NewGuid().ToString("N");

        context.TraceIdentifier = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using var scope = logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId });
        var timer = Stopwatch.StartNew();

        try
        {
            await next(context);
        }
        finally
        {
            logger.LogInformation(
                "HTTP {Method} {Path} respondió {Status} en {ElapsedMs} ms",
                context.Request.Method,
                context.Request.Path.Value,
                context.Response.StatusCode,
                timer.ElapsedMilliseconds);
        }
    }

    [GeneratedRegex("^[A-Za-z0-9_-]{1,64}$")]
    private static partial Regex ValidId();
}

internal sealed class HttpCorrelationIdAccessor(IHttpContextAccessor accessor) : ICorrelationIdAccessor
{
    public string? CorrelationId => accessor.HttpContext?.TraceIdentifier;
}
