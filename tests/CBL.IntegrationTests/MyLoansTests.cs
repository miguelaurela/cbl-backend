using System.Net;
using System.Net.Http.Json;
using CBL.IntegrationTests.Support;

namespace CBL.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class MyLoansTests(DatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    private async Task<(HttpClient Client, Guid LoanId)> BorrowerWithLoanAsync()
    {
        var client = await Factory.CreateAuthorizedClientAsync(TestUsers.BorrowerEmail);

        return (client, await client.CreateLoanAsync());
    }

    [SqlServerFact]
    public async Task A_borrower_sees_only_their_own_loans()
    {
        var other = await Factory.AddBorrowerAsync("otro@test.com");
        var mine = await Factory.CreateAuthorizedClientAsync(TestUsers.BorrowerEmail);
        var theirs = await Factory.CreateAuthorizedClientAsync(other.Email);
        var myLoan = await mine.CreateLoanAsync(1500m);
        await theirs.CreateLoanAsync(9000m);

        var response = await mine.GetAsync("/api/loans/mine");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = (await response.ReadJsonAsync()).EnumerateArray().ToList();
        list.Should().ContainSingle();
        list[0].GetProperty("id").GetGuid().Should().Be(myLoan);
        list[0].GetProperty("borrowerEmail").GetString().Should().Be(TestUsers.BorrowerEmail);
    }

    [SqlServerFact]
    public async Task Another_borrowers_loan_looks_nonexistent_for_detail_status_edit_and_delete()
    {
        var (_, loanId) = await BorrowerWithLoanAsync();
        var other = await Factory.AddBorrowerAsync();
        var intruder = await Factory.CreateAuthorizedClientAsync(other.Email);

        (await intruder.GetAsync($"/api/loans/{loanId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await intruder.GetAsync($"/api/loans/{loanId}/status")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await intruder.PutAsJsonAsync($"/api/loans/{loanId}", new { amount = 2000m, termMonths = 6 })).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
        (await intruder.DeleteAsync($"/api/loans/{loanId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [SqlServerFact]
    public async Task The_cached_status_of_one_borrower_is_never_served_to_another()
    {
        var (owner, loanId) = await BorrowerWithLoanAsync();
        (await owner.GetAsync($"/api/loans/{loanId}/status")).StatusCode.Should().Be(HttpStatusCode.OK);   // llena la caché

        var other = await Factory.AddBorrowerAsync();
        var intruder = await Factory.CreateAuthorizedClientAsync(other.Email);

        (await intruder.GetAsync($"/api/loans/{loanId}/status")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [SqlServerFact]
    public async Task An_admin_can_read_any_loan_and_its_status()
    {
        var (_, loanId) = await BorrowerWithLoanAsync();
        var admin = await Factory.CreateAuthorizedClientAsync(TestUsers.AdminEmail);

        (await admin.GetAsync($"/api/loans/{loanId}")).StatusCode.Should().Be(HttpStatusCode.OK);
        var status = await admin.GetAsync($"/api/loans/{loanId}/status");
        status.StatusCode.Should().Be(HttpStatusCode.OK);
        (await status.ReadJsonAsync()).GetProperty("status").GetString().Should().Be("Pending");
    }

    [SqlServerFact]
    public async Task The_owner_edits_a_pending_loan()
    {
        var (client, loanId) = await BorrowerWithLoanAsync();

        var response = await client.PutAsJsonAsync($"/api/loans/{loanId}", new { amount = 7500m, termMonths = 24 });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.ReadJsonAsync();
        body.GetProperty("amount").GetDecimal().Should().Be(7500m);
        body.GetProperty("termMonths").GetInt32().Should().Be(24);
        body.GetProperty("version").GetInt32().Should().Be(2);
    }

    [SqlServerFact]
    public async Task Editing_revalidates_the_limits_and_the_term()
    {
        var (client, loanId) = await BorrowerWithLoanAsync();

        var tooBig = await client.PutAsJsonAsync($"/api/loans/{loanId}", new { amount = 200000m, termMonths = 12 });
        var badTerm = await client.PutAsJsonAsync($"/api/loans/{loanId}", new { amount = 5000m, termMonths = 99 });

        tooBig.StatusCode.Should().Be((HttpStatusCode)422);
        badTerm.StatusCode.Should().Be((HttpStatusCode)422);
    }

    [SqlServerFact]
    public async Task A_decided_loan_cannot_be_edited_or_deleted_by_its_owner()
    {
        var (client, loanId) = await BorrowerWithLoanAsync();
        var admin = await Factory.CreateAuthorizedClientAsync(TestUsers.AdminEmail);
        (await admin.ApproveAsync(loanId)).StatusCode.Should().Be(HttpStatusCode.OK);

        var edit = await client.PutAsJsonAsync($"/api/loans/{loanId}", new { amount = 7500m, termMonths = 24 });
        var delete = await client.DeleteAsync($"/api/loans/{loanId}");

        edit.StatusCode.Should().Be(HttpStatusCode.Conflict);
        edit.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        delete.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [SqlServerFact]
    public async Task The_owner_deletes_a_pending_loan_and_it_disappears()
    {
        var (client, loanId) = await BorrowerWithLoanAsync();

        (await client.DeleteAsync($"/api/loans/{loanId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await client.GetAsync($"/api/loans/{loanId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.GetAsync($"/api/loans/{loanId}/status")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [SqlServerFact]
    public async Task An_admin_cannot_use_the_borrowers_own_listing_or_edit_endpoint()
    {
        var (_, loanId) = await BorrowerWithLoanAsync();
        var admin = await Factory.CreateAuthorizedClientAsync(TestUsers.AdminEmail);

        (await admin.GetAsync("/api/loans/mine")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await admin.PutAsJsonAsync($"/api/loans/{loanId}", new { amount = 2000m, termMonths = 6 })).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
    }

    [SqlServerFact]
    public async Task Without_a_token_the_borrower_routes_are_unauthenticated()
    {
        var client = Factory.CreateClient();

        (await client.GetAsync("/api/loans/mine")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync($"/api/loans/{Guid.NewGuid()}/status")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
