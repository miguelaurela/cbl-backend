using System.Text.Json.Serialization;
using CBL.Application.Common.Exceptions;
using FluentValidation;

namespace CBL.Api.Extensions;

/// <summary>Ejecuta el validador de FluentValidation antes del caso de uso: un cuerpo inválido nunca lo alcanza.</summary>
public sealed class ValidationFilter<T> : IEndpointFilter where T : class
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var request = context.Arguments.OfType<T>().FirstOrDefault();

        if (request is not null)
        {
            var validator = context.HttpContext.RequestServices.GetService<IValidator<T>>();

            if (validator is not null)
            {
                var result = await validator.ValidateAsync(request, context.HttpContext.RequestAborted);

                if (!result.IsValid)
                {
                    throw ValidationFailedException.FromResult(result);
                }
            }
        }

        return await next(context);
    }
}

public static class ValidationExtensions
{
    public static RouteHandlerBuilder WithValidation<T>(this RouteHandlerBuilder builder) where T : class =>
        builder.AddEndpointFilter<ValidationFilter<T>>();

    /// <summary>JSON en camelCase (por defecto en la web) y enumeraciones como cadenas.</summary>
    public static IServiceCollection AddCblJson(this IServiceCollection services) =>
        services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
}
