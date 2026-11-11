using System.Text;
using CBL.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CBL.Api.Extensions;

public static class Policies
{
    public const string AdminOnly = "AdminOnly";

    public const string BorrowerOnly = "BorrowerOnly";
}

public static class AuthExtensions
{
    /// <summary>
    /// JWT Bearer (emisor, audiencia, firma y expiración; <c>ClockSkew</c> de 30 s), policies por rol y política de
    /// respaldo "usuario autenticado": todo endpoint es privado salvo el que se marque <c>AllowAnonymous</c> (el login).
    /// </summary>
    public static IServiceCollection AddCblAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<JwtOptions>((options, jwt) =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "sub",
                    RoleClaimType = "role"
                };
            });

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(Policies.AdminOnly, policy => policy.RequireRole("Admin"))
            .AddPolicy(Policies.BorrowerOnly, policy => policy.RequireRole("Borrower"));

        return services;
    }
}
