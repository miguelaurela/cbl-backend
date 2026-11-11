using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using CBL.IntegrationTests.Support;

namespace CBL.IntegrationTests;

/// <summary>
/// Pipeline de middleware de seguridad (constitución, principio III): cabeceras en toda respuesta, correlation ID, CORS
/// restringido al frontend, límite de cuerpo, rate limiting global y logs sin datos sensibles.
/// </summary>
[Trait("Category", "Integration")]
public sealed class SecurityPipelineTests(DatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    private static void AssertSecurityHeaders(HttpResponseMessage response)
    {
        response.Headers.GetValues("X-Content-Type-Options").Should().Contain("nosniff");
        response.Headers.GetValues("X-Frame-Options").Should().Contain("DENY");
        response.Headers.GetValues("Referrer-Policy").Should().Contain("no-referrer");
        response.Headers.GetValues("Cache-Control").Should().Contain("no-store");
        response.Headers.GetValues("Content-Security-Policy").Should().ContainSingle()
            .Which.Should().Contain("default-src 'none'").And.Contain("frame-ancestors 'none'");
    }

    [SqlServerFact]
    public async Task Security_headers_are_present_on_success_and_on_every_kind_of_error()
    {
        var authorized = await Factory.CreateAuthorizedClientAsync(TestUsers.BorrowerEmail);
        var anonymous = Factory.CreateClient();

        AssertSecurityHeaders(await authorized.GetAsync("/api/loans/mine"));                                      // 200
        AssertSecurityHeaders(await anonymous.GetAsync("/api/loans/mine"));                                       // 401
        AssertSecurityHeaders(await authorized.GetAsync("/api/loans"));                                           // 403
        AssertSecurityHeaders(await authorized.GetAsync($"/api/loans/{Guid.NewGuid()}"));                         // 404
        AssertSecurityHeaders(await anonymous.PostAsJsonAsync("/api/auth/login", new { email = "", password = "" })); // 422
        AssertSecurityHeaders(await anonymous.PostAsync("/api/auth/login", new StringContent("{ roto", Encoding.UTF8, "application/json"))); // 400
    }

    [SqlServerFact]
    public async Task The_correlation_id_is_echoed_and_becomes_the_trace_id_of_errors()
    {
        var client = Factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new { email = "", password = "" })
        };
        request.Headers.Add("X-Correlation-Id", "prueba-correlacion-123");

        var response = await client.SendAsync(request);

        response.Headers.GetValues("X-Correlation-Id").Should().Equal("prueba-correlacion-123");
        (await response.ReadJsonAsync()).GetProperty("traceId").GetString().Should().Be("prueba-correlacion-123");
    }

    [SqlServerFact]
    public async Task Every_error_carries_a_trace_id_even_the_ones_without_a_body_from_the_framework()
    {
        var anonymous = Factory.CreateClient();
        var authorized = await Factory.CreateAuthorizedClientAsync(TestUsers.BorrowerEmail);

        foreach (var response in new[]
                 {
                     await anonymous.GetAsync("/api/loans/mine"),                         // 401 de la autenticación
                     await authorized.GetAsync("/api/loans"),                             // 403 de la autorización
                     await authorized.GetAsync($"/api/loans/{Guid.NewGuid()}"),           // 404 del caso de uso
                     await anonymous.PostAsJsonAsync("/api/auth/login", new { email = "", password = "" }) // 422
                 })
        {
            var body = await response.ReadJsonAsync();
            body.TryGetProperty("traceId", out var traceId).Should().BeTrue($"el {(int)response.StatusCode} debe llevar traceId");
            traceId.GetString().Should().NotBeNullOrEmpty();
            traceId.GetString().Should().Be(response.Headers.GetValues("X-Correlation-Id").Single());
        }
    }

    [SqlServerFact]
    public async Task An_invalid_or_missing_correlation_id_is_replaced_by_a_generated_one()
    {
        var client = Factory.CreateClient();
        using var invalid = new HttpRequestMessage(HttpMethod.Get, "/api/loans/mine");
        invalid.Headers.Add("X-Correlation-Id", "con espacios y <caracteres> raros");

        var withInvalid = await client.SendAsync(invalid);
        var withoutHeader = await client.GetAsync("/api/loans/mine");

        foreach (var response in new[] { withInvalid, withoutHeader })
        {
            var id = response.Headers.GetValues("X-Correlation-Id").Single();
            id.Should().MatchRegex("^[A-Za-z0-9_-]{1,64}$");
            id.Should().NotContain(" ");
        }
    }

    [SqlServerFact]
    public async Task Only_the_frontend_origin_receives_cors_headers_and_credentials_are_never_allowed()
    {
        var client = Factory.CreateClient();

        using var allowed = new HttpRequestMessage(HttpMethod.Get, "/api/loans/mine");
        allowed.Headers.Add("Origin", "http://localhost:4200");
        var allowedResponse = await client.SendAsync(allowed);
        allowedResponse.Headers.GetValues("Access-Control-Allow-Origin").Should().Equal("http://localhost:4200");
        allowedResponse.Headers.Contains("Access-Control-Allow-Credentials").Should().BeFalse();

        using var denied = new HttpRequestMessage(HttpMethod.Get, "/api/loans/mine");
        denied.Headers.Add("Origin", "http://sitio-malo.example");
        var deniedResponse = await client.SendAsync(denied);
        deniedResponse.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }

    [SqlServerFact]
    public async Task The_preflight_from_the_frontend_allows_authorization_and_the_documented_methods()
    {
        var client = Factory.CreateClient();
        using var preflight = new HttpRequestMessage(HttpMethod.Options, "/api/loans");
        preflight.Headers.Add("Origin", "http://localhost:4200");
        preflight.Headers.Add("Access-Control-Request-Method", "POST");
        preflight.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");

        var response = await client.SendAsync(preflight);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        response.Headers.GetValues("Access-Control-Allow-Headers").Single().ToLowerInvariant().Should().Contain("authorization");
        response.Headers.GetValues("Access-Control-Allow-Methods").Single().Should().Contain("POST");
        response.Headers.Contains("Access-Control-Allow-Credentials").Should().BeFalse();
    }

    [SqlServerFact]
    public async Task A_body_over_64_kb_is_rejected_with_413_problem_json()
    {
        var oversized = new StringContent(new string('a', 70 * 1024), Encoding.UTF8, "application/json");

        var response = await Factory.CreateClient().PostAsync("/api/auth/login", oversized);

        response.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        AssertSecurityHeaders(response);
    }

    [SqlServerFact]
    public async Task A_body_just_under_the_limit_still_reaches_the_endpoint()
    {
        var almost = new StringContent("{\"email\":\"" + new string('a', 60 * 1024) + "\",\"password\":\"x\"}", Encoding.UTF8, "application/json");

        var response = await Factory.CreateClient().PostAsync("/api/auth/login", almost);

        response.StatusCode.Should().NotBe(HttpStatusCode.RequestEntityTooLarge);
    }

    [SqlServerFact]
    public async Task The_global_rate_limit_answers_429_with_retry_after_and_problem_json()
    {
        using var limited = new CblWebApplicationFactory(new Dictionary<string, string?> { ["RateLimiting:GlobalPerMinute"] = "5" });
        var client = limited.CreateClient();

        for (var i = 0; i < 5; i++)
        {
            (await client.GetAsync("/api/loans/mine")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        var rejected = await client.GetAsync("/api/loans/mine");

        rejected.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        rejected.Headers.RetryAfter.Should().NotBeNull();
        rejected.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }

    [SqlServerFact]
    public async Task Logs_never_contain_passwords_tokens_or_authorization_values()
    {
        var provider = new CapturingLoggerProvider();
        using var logged = new CblWebApplicationFactory(loggerProvider: provider);
        var client = logged.CreateClient();
        const string secretPassword = "ClaveSecreta-NoDebeVerseEnLogs-987";

        await client.PostAsJsonAsync("/api/auth/login", new { email = TestUsers.BorrowerEmail, password = secretPassword });
        var login = await AuthHelper.LoginAsync(client, TestUsers.BorrowerEmail);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        await client.GetAsync("/api/loans/mine");
        await client.PostAsync("/api/auth/login", new StringContent("{ roto " + secretPassword, Encoding.UTF8, "application/json"));
        await client.GetAsync("/api/loans/mine?token=" + login.AccessToken);

        var log = string.Join('\n', provider.Lines);

        provider.Lines.Should().NotBeEmpty("la API sí registra las peticiones");
        log.Should().NotContain(secretPassword);
        log.Should().NotContain(login.AccessToken);
        log.Should().NotMatchRegex("eyJ[A-Za-z0-9_-]+\\.[A-Za-z0-9_-]+\\.[A-Za-z0-9_-]+", "ninguna cadena con forma de JWT");
        log.Should().NotMatchRegex("(?i)authorization:\\s*bearer", "ninguna cabecera Authorization con su valor");
        log.Should().Contain("HTTP GET /api/loans/mine respondió");
    }
}
