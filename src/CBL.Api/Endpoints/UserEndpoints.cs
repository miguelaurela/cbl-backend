using CBL.Api.Extensions;
using CBL.Application.Users;

namespace CBL.Api.Endpoints;

/// <summary>Gestión de usuarios: solo Admin. La baja es lógica (PUT con <c>isActive=false</c>); no hay DELETE.</summary>
public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users")
            .WithTags("Users")
            .RequireAuthorization(Policies.AdminOnly);

        group.MapGet(string.Empty, async (GetUsersHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(ct)))
            .WithSummary("Listar usuarios (FR-020)");

        group.MapPost(string.Empty, async (CreateUserRequest request, CreateUserHandler handler, CancellationToken ct) =>
            {
                var user = await handler.HandleAsync(request, ct);

                return TypedResults.Created($"/api/users/{user.Id}", user);
            })
            .WithValidation<CreateUserRequest>()
            .WithSummary("Crear usuario (FR-020, FR-021)");

        group.MapGet("/{id:guid}", async (Guid id, GetUserByIdHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(id, ct)))
            .WithSummary("Consultar un usuario (FR-020)");

        group.MapPut("/{id:guid}", async (Guid id, UpdateUserRequest request, UpdateUserHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(id, request, ct)))
            .WithValidation<UpdateUserRequest>()
            .WithSummary("Actualizar o desactivar un usuario (FR-020, FR-022)");

        return app;
    }
}
