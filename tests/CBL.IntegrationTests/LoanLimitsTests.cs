using System.Net;
using System.Net.Http.Json;
using CBL.IntegrationTests.Support;

namespace CBL.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class LoanLimitsTests(DatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    private static Task<HttpResponseMessage> SetLimitsAsync(HttpClient admin, decimal min, decimal max) =>
        admin.PutAsJsonAsync("/api/parameters/loan-limits", new { minAmount = min, maxAmount = max });

    [SqlServerFact]
    public async Task Any_authenticated_user_reads_the_current_limits()
    {
        foreach (var email in new[] { TestUsers.BorrowerEmail, TestUsers.AdminEmail })
        {
            var client = await Factory.CreateAuthorizedClientAsync(email);

            var response = await client.GetAsync("/api/parameters/loan-limits");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await response.ReadJsonAsync();
            body.GetProperty("minAmount").GetDecimal().Should().Be(1000m);
            body.GetProperty("maxAmount").GetDecimal().Should().Be(100000m);
        }
    }

    [SqlServerFact]
    public async Task Reading_the_limits_without_a_token_is_unauthenticated()
    {
        (await Factory.CreateClient().GetAsync("/api/parameters/loan-limits")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [SqlServerFact]
    public async Task A_borrower_cannot_change_the_limits()
    {
        var borrower = await Factory.CreateAuthorizedClientAsync(TestUsers.BorrowerEmail);

        var response = await SetLimitsAsync(borrower, 1m, 2m);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }

    [SqlServerFact]
    public async Task Changing_the_limits_applies_to_the_next_request_without_redeploying()
    {
        var borrower = await Factory.CreateAuthorizedClientAsync(TestUsers.BorrowerEmail);
        var admin = await Factory.CreateAuthorizedClientAsync(TestUsers.AdminEmail);
        (await borrower.RequestLoanAsync(1500m, 12)).StatusCode.Should().Be(HttpStatusCode.Created);   // llena la caché de límites

        var change = await SetLimitsAsync(admin, 2000m, 50000m);
        change.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await change.ReadJsonAsync();
        body.GetProperty("minAmount").GetDecimal().Should().Be(2000m);
        body.GetProperty("maxAmount").GetDecimal().Should().Be(50000m);

        (await borrower.RequestLoanAsync(1500m, 12)).StatusCode.Should().Be((HttpStatusCode)422);
        (await borrower.RequestLoanAsync(60000m, 12)).StatusCode.Should().Be((HttpStatusCode)422);
        (await borrower.RequestLoanAsync(2000m, 12)).StatusCode.Should().Be(HttpStatusCode.Created);
        (await borrower.RequestLoanAsync(50000m, 12)).StatusCode.Should().Be(HttpStatusCode.Created);

        // La lectura también refleja el cambio enseguida (caché invalidada).
        var read = await (await borrower.GetAsync("/api/parameters/loan-limits")).ReadJsonAsync();
        read.GetProperty("minAmount").GetDecimal().Should().Be(2000m);
    }

    [SqlServerFact]
    public async Task Loans_registered_before_the_change_keep_their_amount()
    {
        var borrower = await Factory.CreateAuthorizedClientAsync(TestUsers.BorrowerEmail);
        var admin = await Factory.CreateAuthorizedClientAsync(TestUsers.AdminEmail);
        var loanId = await borrower.CreateLoanAsync(1500m);

        (await SetLimitsAsync(admin, 2000m, 50000m)).StatusCode.Should().Be(HttpStatusCode.OK);

        var loan = await (await borrower.GetAsync($"/api/loans/{loanId}")).ReadJsonAsync();
        loan.GetProperty("amount").GetDecimal().Should().Be(1500m);
    }

    [SqlServerFact]
    public async Task Invalid_limits_return_422_with_the_offending_fields()
    {
        var admin = await Factory.CreateAuthorizedClientAsync(TestUsers.AdminEmail);

        var zero = await SetLimitsAsync(admin, 0m, 100m);
        var inverted = await SetLimitsAsync(admin, 500m, 500m);

        zero.StatusCode.Should().Be((HttpStatusCode)422);
        (await zero.ReadJsonAsync()).GetProperty("errors").TryGetProperty("minAmount", out _).Should().BeTrue();
        inverted.StatusCode.Should().Be((HttpStatusCode)422);
        (await inverted.ReadJsonAsync()).GetProperty("errors").TryGetProperty("maxAmount", out _).Should().BeTrue();

        var unchanged = await (await admin.GetAsync("/api/parameters/loan-limits")).ReadJsonAsync();
        unchanged.GetProperty("minAmount").GetDecimal().Should().Be(1000m);
    }
}
