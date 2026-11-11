namespace CBL.Api.Endpoints;

public static class EndpointRegistration
{
    /// <summary>Registra los grupos de endpoints (delgados: petición → caso de uso → DTO). Cada historia añade el suyo.</summary>
    public static IEndpointRouteBuilder MapCblEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapAuthEndpoints();

        return app;
    }
}
