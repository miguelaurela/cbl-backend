using Microsoft.AspNetCore.Mvc;

namespace CBL.Api.Middleware;

/// <summary>
/// Rechaza con 413 (ProblemDetails) las peticiones cuyo cuerpo declarado supera el máximo (64 KB). Kestrel también
/// aplica el mismo límite para cuerpos sin <c>Content-Length</c> (chunked); esta comprobación explícita responde igual
/// en cualquier servidor y es verificable en pruebas.
/// </summary>
public sealed class RequestBodyLimitMiddleware(RequestDelegate next, IProblemDetailsService problems)
{
    public const long MaxBytes = 64 * 1024;

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.ContentLength is > MaxBytes)
        {
            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;

            await problems.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = context,
                ProblemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status413PayloadTooLarge,
                    Title = "Cuerpo demasiado grande.",
                    Detail = $"El cuerpo de la solicitud no puede superar {MaxBytes / 1024} KB."
                }
            });

            return;
        }

        await next(context);
    }
}
