using CBL.Api.Extensions;
using CBL.Application.Auth.Login;

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
            .WithSummary("Iniciar sesión (FR-001, FR-003)");

        return app;
    }
}
