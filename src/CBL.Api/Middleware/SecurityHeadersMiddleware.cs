namespace CBL.Api.Middleware;

/// <summary>Cabeceras de seguridad en TODA respuesta, incluidas las de error.</summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next, IHostEnvironment environment)
{
    private const string StrictCsp = "default-src 'none'; frame-ancestors 'none'";

    // Swagger UI necesita scripts y estilos propios; se relaja SOLO en Development.
    private const string SwaggerCsp =
        "default-src 'self'; img-src 'self' data:; style-src 'self' 'unsafe-inline'; script-src 'self' 'unsafe-inline'; frame-ancestors 'none'";

    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers["Cache-Control"] = "no-store";
            headers["Content-Security-Policy"] =
                environment.IsDevelopment() && context.Request.Path.StartsWithSegments("/swagger") ? SwaggerCsp : StrictCsp;
            return Task.CompletedTask;
        });

        return next(context);
    }
}
