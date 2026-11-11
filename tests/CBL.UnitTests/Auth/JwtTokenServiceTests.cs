using System.IdentityModel.Tokens.Jwt;
using System.Text;
using CBL.Domain.Users;
using CBL.Infrastructure.Security;
using CBL.UnitTests.Support;
using Microsoft.IdentityModel.Tokens;

namespace CBL.UnitTests.Auth;

public class JwtTokenServiceTests
{
    private const string Key = "clave-solo-para-pruebas-0123456789-ABCDEFGHIJ";

    private readonly FakeClock _clock = new();

    private JwtTokenService Create(string key = Key) =>
        new(new JwtOptions { Issuer = "cbl-api", Audience = "cbl-clients", Key = key, LifetimeMinutes = 10 }, _clock);

    [Fact]
    public void Token_carries_sub_email_role_and_jti_and_expires_in_ten_minutes()
    {
        var user = Builders.NewUser(Role.Admin, "admin@test.com");

        var issued = Create().Issue(user);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(issued.AccessToken);

        jwt.Claims.Single(c => c.Type == "sub").Value.Should().Be(user.Id.ToString());
        jwt.Claims.Single(c => c.Type == "email").Value.Should().Be("admin@test.com");
        jwt.Claims.Single(c => c.Type == "role").Value.Should().Be("Admin");
        jwt.Claims.Single(c => c.Type == "jti").Value.Should().Be(issued.TokenId);
        jwt.ValidTo.Should().Be(_clock.UtcNow.AddMinutes(10));
        issued.ExpiresAtUtc.Should().Be(_clock.UtcNow.AddMinutes(10));
        jwt.Issuer.Should().Be("cbl-api");
        jwt.Audiences.Should().Contain("cbl-clients");
    }

    [Fact]
    public void Token_is_signed_with_hs256_and_validates_with_the_same_key_only()
    {
        var issued = Create().Issue(Builders.NewUser());
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(issued.AccessToken);
        jwt.Header.Alg.Should().Be("HS256");

        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = false,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key))
        };

        new JwtSecurityTokenHandler().Invoking(h => h.ValidateToken(issued.AccessToken, parameters, out _)).Should().NotThrow();

        parameters.IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("otra-clave-distinta-0123456789-ABCDEFGHIJKL"));
        new JwtSecurityTokenHandler().Invoking(h => h.ValidateToken(issued.AccessToken, parameters, out _))
            .Should().Throw<SecurityTokenException>();
    }

    [Fact]
    public void Every_token_has_a_unique_jti()
    {
        var sut = Create();
        var user = Builders.NewUser();

        sut.Issue(user).TokenId.Should().NotBe(sut.Issue(user).TokenId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("demasiado-corta")]
    public void A_missing_or_short_key_is_rejected_at_startup(string key)
    {
        var options = new JwtOptions { Issuer = "cbl-api", Audience = "cbl-clients", Key = key };

        options.Invoking(o => o.Validate()).Should().Throw<InvalidOperationException>().WithMessage("*Jwt:Key*");
    }

    [Fact]
    public void A_key_of_at_least_32_bytes_is_accepted()
    {
        var options = new JwtOptions { Issuer = "cbl-api", Audience = "cbl-clients", Key = Key };

        options.Invoking(o => o.Validate()).Should().NotThrow();
    }
}
