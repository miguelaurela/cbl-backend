using CBL.Api.Middleware;
using CBL.Api.Security;
using CBL.Application.Abstractions;

namespace CBL.Api.Extensions;

public static class ApiServiceExtensions
{
    public static IServiceCollection AddCblApi(this IServiceCollection services, IHostEnvironment environment)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<ICorrelationIdAccessor, HttpCorrelationIdAccessor>();

        services.AddProblemDetails();
        services.AddExceptionHandler<ApiExceptionHandler>();

        services.AddCblJson();
        services.AddCblCors();
        services.AddCblRateLimiting();
        services.AddCblAuthentication();

        if (environment.IsDevelopment())
        {
            services.AddCblSwagger();
        }

        return services;
    }
}
