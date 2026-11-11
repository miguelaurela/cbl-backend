using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CBL.Application.Abstractions;
using CBL.Domain.Users;
using Microsoft.IdentityModel.Tokens;

namespace CBL.Infrastructure.Security;

/// <summary>JWT HS256 de vida corta (10 minutos) con los claims sub, email, role y jti; sin refresh token.</summary>
public sealed class JwtTokenService(JwtOptions options, IClock clock) : ITokenService
{
    private readonly SigningCredentials _credentials = new(
        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Key)),
        SecurityAlgorithms.HmacSha256);

    public IssuedToken Issue(User user)
    {
        var now = clock.UtcNow;
        var expires = now.AddMinutes(options.LifetimeMinutes);
        var tokenId = Guid.NewGuid().ToString("N");

        var claims = new List<Claim>
        {
            new("sub", user.Id.ToString()),
            new("email", user.Email),
            new("role", user.Role.ToString()),
            new("jti", tokenId)
        };

        var token = new JwtSecurityToken(options.Issuer, options.Audience, claims, now, expires, _credentials);

        return new IssuedToken(new JwtSecurityTokenHandler().WriteToken(token), expires, tokenId);
    }
}
