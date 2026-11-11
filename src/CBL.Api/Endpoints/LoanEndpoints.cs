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
        var group = app.MapGroup("/api/loans").WithTags("Loans");

        // US2 – solicitar (solo Borrower).
        group.MapPost(string.Empty, async (CreateLoanRequest request, RequestLoanHandler handler, CancellationToken ct) =>
            {
                var loan = await handler.HandleAsync(request, ct);

                return TypedResults.Created($"/api/loans/{loan.Id}", loan);
            })
            .RequireAuthorization(Policies.BorrowerOnly)
            .WithValidation<CreateLoanRequest>()
            .WithSummary("Solicitar un préstamo (FR-006 a FR-010)");

        // US3 – mis préstamos (solo Borrower).
        group.MapGet("/mine", async (GetMyLoansHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(ct)))
            .RequireAuthorization(Policies.BorrowerOnly)
            .WithSummary("Mis préstamos (FR-011)");

        // US4 – todos los préstamos (solo Admin), filtro opcional por estado y sin paginación.
        group.MapGet(string.Empty, async (LoanStatus? status, GetAllLoansHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(status, ct)))
            .RequireAuthorization(Policies.AdminOnly)
            .WithSummary("Ver todos los préstamos (FR-013)");

        // US3 – detalle y estado (dueño o Admin; el caso de uso responde 404 a los demás).
        group.MapGet("/{id:guid}", async (Guid id, GetLoanByIdHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(id, ct)))
            .WithSummary("Detalle de un préstamo (dueño o Admin)");

        group.MapGet("/{id:guid}/status", async (Guid id, GetLoanStatusHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(id, ct)))
            .WithSummary("Estado de un préstamo, con caché (FR-018, FR-019)");

        // US3 – editar y eliminar mientras esté Pendiente (FR-012).
        group.MapPut("/{id:guid}", async (Guid id, CreateLoanRequest request, UpdateLoanHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(id, request, ct)))
            .RequireAuthorization(Policies.BorrowerOnly)
            .WithValidation<CreateLoanRequest>()
            .WithSummary("Editar mi préstamo mientras está Pending (FR-012)");

        group.MapDelete("/{id:guid}", async (Guid id, DeleteLoanHandler handler, CancellationToken ct) =>
            {
                await handler.HandleAsync(id, ct);

                return TypedResults.NoContent();
            })
            .WithSummary("Eliminar un préstamo Pending (dueño o Admin; FR-012)");

        // US5 – decidir (solo Admin). Cuerpo opcional { "version": n }.
        group.MapPost("/{id:guid}/approve", async (Guid id, DecideLoanRequest? request, ApproveLoanHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(id, request?.Version, ct)))
            .RequireAuthorization(Policies.AdminOnly)
            .WithSummary("Aprobar un préstamo Pending (FR-014 a FR-017)");

        group.MapPost("/{id:guid}/reject", async (Guid id, DecideLoanRequest? request, RejectLoanHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(id, request?.Version, ct)))
            .RequireAuthorization(Policies.AdminOnly)
            .WithSummary("Rechazar un préstamo Pending (FR-014 a FR-017)");

        return app;
    }
}
