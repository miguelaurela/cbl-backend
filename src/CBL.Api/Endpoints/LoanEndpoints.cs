using CBL.Api.Extensions;
using CBL.Application.Loans;
using CBL.Application.Loans.Commands;
using CBL.Application.Loans.Queries;
using CBL.Domain.Loans;

namespace CBL.Api.Endpoints;

/// <summary>
/// Endpoints delgados: petición → caso de uso → DTO. Toda la autorización por rol se declara aquí con policies;
/// la propiedad del recurso (dueño o Admin) la resuelve el caso de uso y responde 404 para no permitir enumerar.
/// </summary>
public static class LoanEndpoints
{
    public static IEndpointRouteBuilder MapLoanEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/loans")
            .WithTags("Loans")
            .ProducesProblems(401, 429);

        // US2 – solicitar (solo Borrower).
        group.MapPost(string.Empty, async (CreateLoanRequest request, RequestLoanHandler handler, CancellationToken ct) =>
            {
                var loan = await handler.HandleAsync(request, ct);

                return TypedResults.Created($"/api/loans/{loan.Id}", loan);
            })
            .RequireAuthorization(Policies.BorrowerOnly)
            .WithValidation<CreateLoanRequest>()
            .WithSummary("Solicitar un préstamo (FR-006 a FR-010)")
            .ProducesProblems(400, 403, 422);

        // US3 – mis préstamos (solo Borrower).
        group.MapGet("/mine", async (GetMyLoansHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(ct)))
            .RequireAuthorization(Policies.BorrowerOnly)
            .WithSummary("Mis préstamos (FR-011)")
            .ProducesProblems(403);

        // US4 – todos los préstamos (solo Admin), filtro opcional por estado y sin paginación.
        group.MapGet(string.Empty, async (LoanStatus? status, GetAllLoansHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(status, ct)))
            .RequireAuthorization(Policies.AdminOnly)
            .WithSummary("Ver todos los préstamos (FR-013)")
            .ProducesProblems(400, 403);

        // US3 – detalle y estado (dueño o Admin; el caso de uso responde 404 a los demás).
        group.MapGet("/{id:guid}", async (Guid id, GetLoanByIdHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(id, ct)))
            .WithSummary("Detalle de un préstamo (dueño o Admin)")
            .ProducesProblems(404);

        group.MapGet("/{id:guid}/status", async (Guid id, GetLoanStatusHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(id, ct)))
            .WithSummary("Estado de un préstamo, con caché (FR-018, FR-019)")
            .ProducesProblems(404);

        // US3 – editar y eliminar mientras esté Pendiente (FR-012).
        group.MapPut("/{id:guid}", async (Guid id, CreateLoanRequest request, UpdateLoanHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(id, request, ct)))
            .RequireAuthorization(Policies.BorrowerOnly)
            .WithValidation<CreateLoanRequest>()
            .WithSummary("Editar mi préstamo mientras está Pending (FR-012)")
            .ProducesProblems(400, 403, 404, 409, 422);

        group.MapDelete("/{id:guid}", async (Guid id, DeleteLoanHandler handler, CancellationToken ct) =>
            {
                await handler.HandleAsync(id, ct);

                return TypedResults.NoContent();
            })
            .WithSummary("Eliminar un préstamo Pending (dueño o Admin; FR-012)")
            .ProducesProblems(403, 404, 409);

        // US5 – decidir (solo Admin). Cuerpo opcional { "version": n }: se lee a mano para que un POST sin cuerpo (o con
        // otro Content-Type y cuerpo vacío) funcione, como promete el contrato, en vez de responder 415.
        group.MapPost("/{id:guid}/approve", async (Guid id, HttpRequest request, ApproveLoanHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(id, await ReadExpectedVersionAsync(request, ct), ct)))
            .RequireAuthorization(Policies.AdminOnly)
            .WithSummary("Aprobar un préstamo Pending (FR-014 a FR-017)")
            .WithDescription("Cuerpo opcional con version; una segunda decisión sobre el mismo préstamo siempre responde 409.")
            .ProducesProblems(403, 404, 409);

        group.MapPost("/{id:guid}/reject", async (Guid id, HttpRequest request, RejectLoanHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(id, await ReadExpectedVersionAsync(request, ct), ct)))
            .RequireAuthorization(Policies.AdminOnly)
            .WithSummary("Rechazar un préstamo Pending (FR-014 a FR-017)")
            .ProducesProblems(403, 404, 409);

        return app;
    }

    // Nota: NO se usa Accepts&lt;T&gt;() ni un parámetro de cuerpo: el enrutador los aplica como restricción y respondería
    // 415 a un POST sin JSON. El cuerpo opcional se documenta en Swagger con OptionalDecisionBodyFilter.

    /// <summary>Versión esperada del cuerpo opcional; sin cuerpo, o sin JSON, no hay comprobación de versión.</summary>
    private static async Task<int?> ReadExpectedVersionAsync(HttpRequest request, CancellationToken ct)
    {
        if (request.ContentLength is 0 || !request.HasJsonContentType())
        {
            return null;
        }

        // Un JSON mal formado lanza JsonException y se traduce a 400.
        return (await request.ReadFromJsonAsync<DecideLoanRequest>(ct))?.Version;
    }
}
