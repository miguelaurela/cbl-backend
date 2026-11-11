using CBL.Api.Extensions;
using CBL.Application.Parameters;

namespace CBL.Api.Endpoints;

public static class ParameterEndpoints
{
    public static IEndpointRouteBuilder MapParameterEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/parameters")
            .WithTags("Parameters")
            .ProducesProblems(401, 429);

        // Cualquier usuario autenticado puede leerlos (el formulario de solicitud valida con ellos).
        group.MapGet("/loan-limits", async (GetLoanLimitsHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(ct)))
            .WithSummary("Consultar los límites de monto (FR-024)");

        group.MapPut("/loan-limits", async (LoanLimits request, UpdateLoanLimitsHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(request, ct)))
            .RequireAuthorization(Policies.AdminOnly)
            .WithValidation<LoanLimits>()
            .WithSummary("Modificar los límites de monto (FR-024)")
            .WithDescription("Se aplica a las solicitudes nuevas sin redesplegar; las ya registradas conservan su monto.")
            .ProducesProblems(400, 403, 422);

        return app;
    }
}
