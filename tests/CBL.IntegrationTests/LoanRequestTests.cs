using System.Net;
using System.Net.Http.Json;
using CBL.IntegrationTests.Support;

namespace CBL.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class LoanRequestTests(DatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    [SqlServerFact]
    public async Task A_valid_request_returns_201_with_location_and_a_pending_loan()
    {
        var client = await Factory.CreateAuthorizedClientAsync(TestUsers.BorrowerEmail);

        var response = await client.RequestLoanAsync(5000m, 12);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.ReadJsonAsync();
        var id = body.GetProperty("id").GetGuid();
        response.Headers.Location!.ToString().Should().EndWith($"/api/loans/{id}");
        body.GetProperty("status").GetString().Should().Be("Pending");
        body.GetProperty("amount").GetDecimal().Should().Be(5000m);
        body.GetProperty("termMonths").GetInt32().Should().Be(12);
        body.GetProperty("borrowerId").GetGuid().Should().Be(TestUsers.BorrowerId);
        body.GetProperty("borrowerEmail").GetString().Should().Be(TestUsers.BorrowerEmail);
        body.GetProperty("version").GetInt32().Should().Be(1);
        body.GetProperty("decidedAt").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
        body.GetProperty("createdAt").GetString().Should().EndWith("Z", "las fechas viajan en UTC");
    }

    [SqlServerFact]
    public async Task The_borrower_comes_from_the_token_never_from_the_body()
    {
        var other = await Factory.AddBorrowerAsync();
        var client = await Factory.CreateAuthorizedClientAsync(TestUsers.BorrowerEmail);

        var response = await client.PostAsJsonAsync("/api/loans", new { amount = 5000m, termMonths = 12, borrowerId = other.Id });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await response.ReadJsonAsync()).GetProperty("borrowerId").GetGuid().Should().Be(TestUsers.BorrowerId);
    }

    [SqlServerFact]
    public async Task Amounts_equal_to_the_limits_are_accepted()
    {
        var client = await Factory.CreateAuthorizedClientAsync(TestUsers.BorrowerEmail);

        (await client.RequestLoanAsync(1000m, 1)).StatusCode.Should().Be(HttpStatusCode.Created);
        (await client.RequestLoanAsync(100000m, 60)).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [SqlServerFact]
    public async Task Amounts_outside_the_limits_return_422_with_the_amount_field()
    {
        var client = await Factory.CreateAuthorizedClientAsync(TestUsers.BorrowerEmail);

        foreach (var amount in new[] { 999.99m, 100000.01m, 0m, -5m })
        {
            var response = await client.RequestLoanAsync(amount, 12);

            response.StatusCode.Should().Be((HttpStatusCode)422, $"monto {amount}");
            response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
            (await response.ReadJsonAsync()).GetProperty("errors").TryGetProperty("amount", out _).Should().BeTrue();
        }
    }

    [SqlServerFact]
    public async Task Invalid_terms_return_422_with_the_term_field()
    {
        var client = await Factory.CreateAuthorizedClientAsync(TestUsers.BorrowerEmail);

        foreach (var term in new[] { 0, 61, -1 })
        {
            var response = await client.RequestLoanAsync(5000m, term);

            response.StatusCode.Should().Be((HttpStatusCode)422, $"plazo {term}");
            (await response.ReadJsonAsync()).GetProperty("errors").TryGetProperty("termMonths", out _).Should().BeTrue();
        }
    }

    [SqlServerFact]
    public async Task Both_invalid_fields_are_reported_together()
    {
        var client = await Factory.CreateAuthorizedClientAsync(TestUsers.BorrowerEmail);

        var response = await client.RequestLoanAsync(0m, 0);

        var errors = (await response.ReadJsonAsync()).GetProperty("errors");
        errors.TryGetProperty("amount", out _).Should().BeTrue();
        errors.TryGetProperty("termMonths", out _).Should().BeTrue();
    }

    [SqlServerFact]
    public async Task An_admin_cannot_request_loans()
    {
        var client = await Factory.CreateAuthorizedClientAsync(TestUsers.AdminEmail);

        var response = await client.RequestLoanAsync(5000m, 12);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }

    [SqlServerFact]
    public async Task Without_a_token_the_request_is_unauthenticated()
    {
        var response = await Factory.CreateClient().RequestLoanAsync(5000m, 12);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
