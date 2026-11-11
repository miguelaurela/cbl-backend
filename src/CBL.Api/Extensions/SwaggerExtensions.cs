using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace CBL.Api.Extensions;

public static class SwaggerExtensions
{
    /// <summary>Swagger SOLO en Development (constitución, principio III).</summary>
    public static IServiceCollection AddCblSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo { Title = "CBL Project (Control Banking Loans) API", Version = "v1" });

            var scheme = new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "JWT obtenido en POST /api/auth/login (vida de 10 minutos).",
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            };

            options.AddSecurityDefinition("Bearer", scheme);
            options.AddSecurityRequirement(new OpenApiSecurityRequirement { [scheme] = [] });
            options.OperationFilter<OptionalDecisionBodyFilter>();
        });

        return services;
    }
}

/// <summary>
/// Documenta el cuerpo OPCIONAL <c>{ "version": n }</c> de aprobar/rechazar. El endpoint lo lee a mano (para que un POST
/// sin cuerpo funcione en vez de responder 415), así que Swashbuckle no lo detecta solo.
/// </summary>
internal sealed class OptionalDecisionBodyFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var path = context.ApiDescription.RelativePath ?? string.Empty;

        if (!path.EndsWith("/approve", StringComparison.Ordinal) && !path.EndsWith("/reject", StringComparison.Ordinal))
        {
            return;
        }

        operation.RequestBody = new OpenApiRequestBody
        {
            Required = false,
            Description = "Opcional. Si trae `version` y no coincide con la versión actual del préstamo, se responde 409.",
            Content =
            {
                ["application/json"] = new OpenApiMediaType
                {
                    Schema = context.SchemaGenerator.GenerateSchema(typeof(CBL.Application.Loans.Commands.DecideLoanRequest), context.SchemaRepository)
                }
            }
        };
    }
}
