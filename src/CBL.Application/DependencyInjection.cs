using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace CBL.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registra los casos de uso (toda clase <c>*Handler</c> del ensamblado, sin MediatR) y los validadores
    /// de FluentValidation. Cada historia añade sus manejadores y validadores sin tocar este archivo.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        foreach (var handler in assembly.GetTypes()
                     .Where(t => t is { IsClass: true, IsAbstract: false, IsPublic: true } && t.Name.EndsWith("Handler", StringComparison.Ordinal)))
        {
            services.AddScoped(handler);
        }

        services.AddValidatorsFromAssembly(assembly, ServiceLifetime.Singleton);

        return services;
    }
}
