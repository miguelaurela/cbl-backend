namespace CBL.Api.Extensions;

public static class OpenApiExtensions
{
    /// <summary>
    /// Documenta en OpenAPI las respuestas de error (<c>application/problem+json</c>) que el endpoint o el grupo puede
    /// devolver. El contrato <c>specs/001-gestion-prestamos/contracts/openapi.yaml</c> debe coincidir (lo verifica una prueba).
    /// </summary>
    public static TBuilder ProducesProblems<TBuilder>(this TBuilder builder, params int[] statusCodes)
        where TBuilder : IEndpointConventionBuilder
    {
        foreach (var status in statusCodes)
        {
            builder.Add(endpoint => endpoint.Metadata.Add(
                new Microsoft.AspNetCore.Http.ProducesResponseTypeMetadata(
                    status, typeof(Microsoft.AspNetCore.Mvc.ProblemDetails), ["application/problem+json"])));
        }

        return builder;
    }
}
