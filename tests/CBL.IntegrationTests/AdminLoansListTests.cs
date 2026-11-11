using System.Net;
using CBL.IntegrationTests.Support;

namespace CBL.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class AdminLoansListTests(DatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    [SqlServerFact]
    public async Task The_admin_sees_the_loans_of_every_borrower_with_borrower_amount_and_status()
    {
        var other = await Factory.AddBorrowerAsync("otro@test.com");
        var mine = await Factory.CreateAuthorizedClientAsync(TestUsers.BorrowerEmail);
        var theirs = await Factory.CreateAuthorizedClientAsync(other.Email);
        await mine.CreateLoanAsync(1500m);
        await theirs.CreateLoanAsync(9000m);

        var admin = await Factory.CreateAuthorizedClientAsync(TestUsers.AdminEmail);
        var response = await admin.GetAsync("/api/loans");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = (await response.ReadJsonAsync()).EnumerateArray().ToList();
        list.Should().HaveCount(2);
        list.Select(l => l.GetProperty("borrowerEmail").GetString()).Should().BeEquivalentTo(TestUsers.BorrowerEmail, "otro@test.com");
        list.Should().OnlyContain(l => l.GetProperty("status").GetString() == "Pending");
        list.Should().OnlyContain(l => !string.IsNullOrEmpty(l.GetProperty("borrowerName").GetString()));
    }

    [SqlServerFact]
    public async Task The_status_filter_returns_only_that_status()
    {
        var borrower = await Factory.CreateAuthorizedClientAsync(TestUsers.BorrowerEmail);
        var admin = await Factory.CreateAuthorizedClientAsync(TestUsers.AdminEmail);
        await borrower.CreateLoanAsync(1000m);
        var toApprove = await borrower.CreateLoanAsync(2000m);
        var toReject = await borrower.CreateLoanAsync(3000m);
        await admin.ApproveAsync(toApprove);
        await admin.RejectAsync(toReject);

        async Task<List<string?>> StatusesAsync(string query) =>
            (await (await admin.GetAsync($"/api/loans{query}")).ReadJsonAsync()).EnumerateArray()
                .Select(l => l.GetProperty("status").GetString()).ToList();

        (await StatusesAsync(string.Empty)).Should().HaveCount(3);
        (await StatusesAsync("?status=Pending")).Should().Equal("Pending");
        (await StatusesAsync("?status=Approved")).Should().Equal("Approved");
        (await StatusesAsync("?status=Rejected")).Should().Equal("Rejected");
    }

    [SqlServerFact]
    public async Task An_unknown_status_value_is_a_bad_request()
    {
        var admin = await Factory.CreateAuthorizedClientAsync(TestUsers.AdminEmail);

        var response = await admin.GetAsync("/api/loans?status=Inventado");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }

    [SqlServerFact]
    public async Task Loans_come_newest_first()
    {
        var borrower = await Factory.CreateAuthorizedClientAsync(TestUsers.BorrowerEmail);
        var first = await borrower.CreateLoanAsync(1000m);
        await Task.Delay(1100);   // datetime2(0): la resolución es de un segundo
        var second = await borrower.CreateLoanAsync(2000m);

        var admin = await Factory.CreateAuthorizedClientAsync(TestUsers.AdminEmail);
        var ids = (await (await admin.GetAsync("/api/loans")).ReadJsonAsync()).EnumerateArray()
            .Select(l => l.GetProperty("id").GetGuid()).ToList();

        ids.Should().Equal(second, first);
    }

    [SqlServerFact]
    public async Task A_borrower_cannot_open_the_admin_list()
    {
        var borrower = await Factory.CreateAuthorizedClientAsync(TestUsers.BorrowerEmail);

        var response = await borrower.GetAsync("/api/loans");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }

    [SqlServerFact]
    public async Task Without_a_token_the_admin_list_is_unauthenticated()
    {
        (await Factory.CreateClient().GetAsync("/api/loans")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
