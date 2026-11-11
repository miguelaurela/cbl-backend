using System.Text.Json;
using CBL.Application.Common.Exceptions;
using CBL.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CBL.Api.Middleware;

/// <summary>Traducción de una excepción a la respuesta HTTP (ProblemDetails, RFC 7807).</summary>
public sealed record ErrorMapping(
    int Status,
    string Title,
    string? Detail = null,
    IReadOnlyDictionary<string, string[]>? Errors = null,
    int? RetryAfterSeconds = null);

/// <summary>
/// Manejo global de errores: todo error se devuelve como <c>application/problem+json</c> con el código HTTP
/// correcto y sin detalles internos (constitución, principio IV).
/// </summary>
public sealed class ApiExceptionHandler(IProblemDetailsService problemDetails, ILogger<ApiExceptionHandler> logger)
    : IExceptionHandler
{
    private const string UnexpectedErrorDetail = "Ocurrió un error inesperado. Inténtalo de nuevo más tarde.";

    /// <summary>Tabla excepción → HTTP (pública para poder verificarla con pruebas unitarias).</summary>
    public static ErrorMapping Map(Exception exception) => exception switch
    {
        ValidationFailedException e => new ErrorMapping(422, "Los datos enviados no son válidos.", e.Message, e.Errors),
        DomainValidationException e => new ErrorMapping(
            422,
            "Los datos enviados no son válidos.",
            e.Message,
            new Dictionary<string, string[]> { [ValidationFailedException.ToCamelCase(e.Field)] = [e.Message] }),
        UnauthorizedException e => new ErrorMapping(401, "No autenticado.", e.Message),
        ForbiddenException e => new ErrorMapping(403, "Acceso denegado.", e.Message),
        NotFoundException e => new ErrorMapping(404, "No encontrado.", e.Message),
        AccountLockedException e => new ErrorMapping(
            423, "Cuenta bloqueada temporalmente.", e.Message, RetryAfterSeconds: (int)Math.Ceiling(e.RetryAfter.TotalSeconds)),
        ConflictException e => new ErrorMapping(409, "Conflicto.", e.Message, e.Errors),
        InvalidLoanTransitionException e => new ErrorMapping(409, "Conflicto.", e.Message),
        LoanNotModifiableException e => new ErrorMapping(409, "Conflicto.", e.Message),
        BadHttpRequestException or JsonException => new ErrorMapping(400, "Solicitud mal formada.", "El cuerpo de la solicitud no es válido."),
        _ => new ErrorMapping(500, "Error interno.", UnexpectedErrorDetail)
    };

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var mapping = Map(exception);

        if (mapping.Status >= 500)
        {
            logger.LogError(exception, "Error no controlado en {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
        }
        else
        {
            logger.LogInformation("Solicitud rechazada con {Status}: {Type}", mapping.Status, exception.GetType().Name);
        }

        httpContext.Response.StatusCode = mapping.Status;

        if (mapping.RetryAfterSeconds is { } seconds)
        {
            httpContext.Response.Headers.RetryAfter = seconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        var problem = new ProblemDetails { Status = mapping.Status, Title = mapping.Title, Detail = mapping.Detail };

        if (mapping.Errors is not null)
        {
            problem.Extensions["errors"] = mapping.Errors;
        }

        var written = await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception
        });

        if (!written)
        {
            problem.Extensions["traceId"] = httpContext.TraceIdentifier;
            await httpContext.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json", cancellationToken);
        }

        return true;
    }
}
