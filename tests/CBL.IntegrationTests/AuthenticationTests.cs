using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CBL.IntegrationTests.Support;
using Microsoft.IdentityModel.Tokens;

namespace CBL.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class AuthenticationTests(DatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    private const string ProtectedRoute = "/api/loans/mine";

    private static string CreateToken(string key, DateTime expires, DateTime notBefore, string issuer = "cbl-api")
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer,
            "cbl-clients",
            [new System.Security.Claims.Claim("sub", Guid.NewGuid().ToString()), new System.Security.Claims.Claim("role", "Borrower")],
            notBefore,
            expires,
            credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>Comprueba código y <c>application/problem+json</c>; devuelve el cuerpo (solo se puede leer una vez).</summary>
    private static async Task<JsonElement> AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status)
    {
        response.StatusCode.Should().Be(status);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetInt32().Should().Be((int)status);

        return body;
    }

    [SqlServerFact]
    public async Task Request_without_a_token_is_unauthenticated()
    {
        var response = await Factory.CreateClient().GetAsync(ProtectedRoute);

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized);
    }

    [SqlServerFact]
    public async Task An_expired_token_is_rejected()
    {
        var client = Factory.CreateClient();
        var token = CreateToken(
            CblWebApplicationFactory.TestJwtKey,
            expires: DateTime.UtcNow.AddMinutes(-5),
            notBefore: DateTime.UtcNow.AddMinutes(-15));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync(ProtectedRoute);

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized);
    }

    [SqlServerFact]
    public async Task A_token_signed_with_another_key_is_rejected()
    {
        var client = Factory.CreateClient();
        var token = CreateToken(
            "otra-clave-distinta-0123456789-ABCDEFGHIJKL",
            expires: DateTime.UtcNow.AddMinutes(10),
            notBefore: DateTime.UtcNow);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        (await client.GetAsync(ProtectedRoute)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [SqlServerFact]
    public async Task A_token_with_another_issuer_is_rejected()
    {
        var client = Factory.CreateClient();
        var token = CreateToken(
            CblWebApplicationFactory.TestJwtKey,
            expires: DateTime.UtcNow.AddMinutes(10),
            notBefore: DateTime.UtcNow,
            issuer: "emisor-ajeno");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        (await client.GetAsync(ProtectedRoute)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [SqlServerFact]
    public async Task Valid_login_returns_token_expiry_in_ten_minutes_and_user()
    {
        var client = Factory.CreateClient();

        var login = await AuthHelper.LoginAsync(client, TestUsers.BorrowerEmail);

        login.TokenType.Should().Be("Bearer");
        login.AccessToken.Should().NotBeNullOrEmpty();
        login.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(10), TimeSpan.FromSeconds(30));
        login.User.Email.Should().Be(TestUsers.BorrowerEmail);
        login.User.Role.Should().Be("Borrower");

        var admin = await AuthHelper.LoginAsync(client, TestUsers.AdminEmail);
        admin.User.Role.Should().Be("Admin");
    }

    [SqlServerFact]
    public async Task Email_with_other_capitalization_signs_in_as_the_same_user()
    {
        var login = await AuthHelper.LoginAsync(Factory.CreateClient(), "  USUARIO@Test.com ");

        login.User.Email.Should().Be(TestUsers.BorrowerEmail);
    }

    [SqlServerFact]
    public async Task Wrong_password_and_unknown_email_return_the_same_generic_401()
    {
        var client = Factory.CreateClient();

        var wrongPassword = await client.PostAsJsonAsync("/api/auth/login", new { email = TestUsers.BorrowerEmail, password = "mala" });
        var unknown = await client.PostAsJsonAsync("/api/auth/login", new { email = "nadie@test.com", password = "mala" });

        var a = await AssertProblemAsync(wrongPassword, HttpStatusCode.Unauthorized);
        var b = await AssertProblemAsync(unknown, HttpStatusCode.Unauthorized);

        a.GetProperty("detail").GetString().Should().Be(b.GetProperty("detail").GetString());
        a.GetProperty("title").GetString().Should().Be(b.GetProperty("title").GetString());
    }

    [SqlServerFact]
    public async Task Five_failures_with_the_same_email_lock_it_with_423_and_retry_after()
    {
        var client = Factory.CreateClient();

        for (var i = 0; i < 5; i++)
        {
            var failed = await client.PostAsJsonAsync("/api/auth/login", new { email = TestUsers.BorrowerEmail, password = "mala" });
            failed.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        var locked = await client.PostAsJsonAsync("/api/auth/login", new { email = TestUsers.BorrowerEmail, password = "mala" });
        await AssertProblemAsync(locked, (HttpStatusCode)423);
        locked.Headers.RetryAfter!.Delta!.Value.TotalSeconds.Should().BeInRange(1, 900);

        // Con la contraseña correcta también sigue bloqueado durante la ventana.
        var rightPassword = await client.PostAsJsonAsync("/api/auth/login", new { email = TestUsers.BorrowerEmail, password = TestUsers.SamplePassword });
        rightPassword.StatusCode.Should().Be((HttpStatusCode)423);
    }

    [SqlServerFact]
    public async Task An_unknown_email_is_locked_too_so_the_lockout_does_not_reveal_existence()
    {
        var client = Factory.CreateClient();
        var email = $"fantasma-{Guid.NewGuid():N}@test.com";

        for (var i = 0; i < 5; i++)
        {
            (await client.PostAsJsonAsync("/api/auth/login", new { email, password = "mala" })).StatusCode
                .Should().Be(HttpStatusCode.Unauthorized);
        }

        (await client.PostAsJsonAsync("/api/auth/login", new { email, password = "mala" })).StatusCode
            .Should().Be((HttpStatusCode)423);
    }

    [SqlServerFact]
    public async Task Empty_fields_fail_validation_with_errors_per_field()
    {
        var response = await Factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { email = "", password = "" });

        var body = await AssertProblemAsync(response, (HttpStatusCode)422);
        var errors = body.GetProperty("errors");
        errors.TryGetProperty("email", out _).Should().BeTrue();
        errors.TryGetProperty("password", out _).Should().BeTrue();
    }

    [SqlServerFact]
    public async Task Malformed_json_returns_400_problem()
    {
        var content = new StringContent("{ esto no es json", Encoding.UTF8, "application/json");

        var response = await Factory.CreateClient().PostAsync("/api/auth/login", content);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
    }

    [SqlServerFact]
    public async Task Login_is_rate_limited_per_ip_with_429_and_retry_after()
    {
        using var limited = new CblWebApplicationFactory(new Dictionary<string, string?> { ["RateLimiting:LoginPerMinute"] = "3" });
        var client = limited.CreateClient();

        for (var i = 0; i < 3; i++)
        {
            var allowed = await client.PostAsJsonAsync("/api/auth/login", new { email = $"rl{i}@test.com", password = "mala" });
            allowed.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        var rejected = await client.PostAsJsonAsync("/api/auth/login", new { email = "rl4@test.com", password = "mala" });

        await AssertProblemAsync(rejected, HttpStatusCode.TooManyRequests);
        rejected.Headers.RetryAfter.Should().NotBeNull();
    }
}
