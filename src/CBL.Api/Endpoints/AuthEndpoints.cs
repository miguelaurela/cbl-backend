using CBL.Api.Extensions;
using CBL.Application.Auth.Login;
using CBL.Application.Auth.Logout;

namespace CBL.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        // Único endpoint anónimo; con política de tasa estricta.
        group.MapPost("/login", async (LoginRequest request, LoginHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(request, ct)))
            .AllowAnonymous()
            .RequireRateLimiting(HttpPipelineExtensions.LoginRateLimitPolicy)
            .WithValidation<LoginRequest>()
            .WithSummary("Iniciar sesión (FR-001, FR-003)")
            .WithDescription("Correo inexistente, contraseña incorrecta y usuario desactivado devuelven el mismo 401 genérico. " +
                             "Tras 5 intentos fallidos con un mismo correo (exista o no) se bloquea 15 minutos (423 con Retry-After).")
            .ProducesProblems(400, 401, 422, 423, 429);

        // Autenticado (política de respaldo): revoca el token actual hasta su expiración.
        group.MapPost("/logout", (LogoutHandler handler) =>
            {
                handler.Handle();

                return TypedResults.NoContent();
            })
            .WithSummary("Cerrar sesión (FR-004)")
            .WithDescription("Revoca el token actual; una vez revocado, el token responde 401 en cualquier endpoint.")
            .ProducesProblems(401, 429);

        return app;
    }
}
