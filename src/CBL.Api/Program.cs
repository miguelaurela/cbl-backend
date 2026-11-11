using CBL.Api.Endpoints;
using CBL.Api.Extensions;
using CBL.Api.Middleware;
using CBL.Application;
using CBL.Application.Abstractions;
using CBL.Application.Auth;
using CBL.Infrastructure;
using CBL.Infrastructure.Persistence;
using CBL.Infrastructure.Security;

var builder = WebApplication.CreateBuilder(args);

// Límite de tamaño de cuerpo: 64 KB.
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 64 * 1024);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Environment.IsDevelopment());
builder.Services.AddCblApi(builder.Environment);

var app = builder.Build();

// Falla rápido si falta la clave JWT o una opción es inválida.
app.Services.GetRequiredService<JwtOptions>();
app.Services.GetRequiredService<AuthOptions>();

if (CBL.Infrastructure.DependencyInjection.ResolveProvider(app.Configuration, app.Environment.IsDevelopment())
    == CBL.Infrastructure.DependencyInjection.InMemoryProvider)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CblDbContext>();
    await db.Database.EnsureCreatedAsync();
    await InMemorySeeder.SeedAsync(db, scope.ServiceProvider.GetRequiredService<IClock>());
}

// Pipeline de middleware en este orden exacto (constitución, principio III):
// manejo global de excepciones → HTTPS/HSTS → cabeceras de seguridad → CORS → rate limiting →
// correlation ID y logging → autenticación → autorización → endpoints.
app.UseExceptionHandler();
app.UseStatusCodePages();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseCors(HttpPipelineExtensions.CorsPolicyName);
app.UseRateLimiter();
app.UseMiddleware<CorrelationIdMiddleware>();

// Swagger SOLO en Development. Va antes de la autorización porque la política de respaldo (todo autenticado)
// también se aplica a rutas sin endpoint y bloquearía la documentación.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapCblEndpoints();

app.Run();

/// <summary>Punto de entrada accesible para WebApplicationFactory en las pruebas de integración.</summary>
public partial class Program;
