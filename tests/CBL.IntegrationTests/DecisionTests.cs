using System.Net;
using CBL.Domain.Loans;
using CBL.IntegrationTests.Support;

namespace CBL.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class DecisionTests(DatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    private async Task<(HttpClient Borrower, HttpClient Admin, Guid LoanId)> ScenarioAsync()
    {
        var borrower = await Factory.CreateAuthorizedClientAsync(TestUsers.BorrowerEmail);
        var admin = await Factory.CreateAuthorizedClientAsync(TestUsers.AdminEmail);

        return (borrower, admin, await borrower.CreateLoanAsync());
    }

    [SqlServerFact]
    public async Task A_borrower_cannot_approve_or_reject()
    {
        var (borrower, _, loanId) = await ScenarioAsync();

        var approve = await borrower.ApproveAsync(loanId);
        var reject = await borrower.RejectAsync(loanId);

        approve.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        approve.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        reject.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Factory.StoredStatusAsync(loanId)).Should().Be(LoanStatus.Pending);
    }

    [SqlServerFact]
    public async Task Without_a_token_deciding_is_unauthenticated()
    {
        var (_, _, loanId) = await ScenarioAsync();

        (await Factory.CreateClient().ApproveAsync(loanId)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [SqlServerFact]
    public async Task Approving_changes_the_status_and_leaves_one_audit_row_with_admin_and_time()
    {
        var (_, admin, loanId) = await ScenarioAsync();

        var response = await admin.ApproveAsync(loanId);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.ReadJsonAsync();
        body.GetProperty("status").GetString().Should().Be("Approved");
        body.GetProperty("decidedById").GetGuid().Should().Be(TestUsers.AdminId);
        body.GetProperty("decidedByName").GetString().Should().NotBeNullOrEmpty();
        body.GetProperty("decidedAt").GetString().Should().EndWith("Z");
        body.GetProperty("version").GetInt32().Should().Be(2);

        var audit = await Factory.AuditRowsAsync(loanId);
        audit.Should().ContainSingle();
        audit[0].AdminUserId.Should().Be(TestUsers.AdminId);
        audit[0].Decision.Should().Be(LoanStatus.Approved);
        audit[0].PreviousStatus.Should().Be(LoanStatus.Pending);
        audit[0].DecidedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        audit[0].CorrelationId.Should().NotBeNullOrEmpty();
    }

    [SqlServerFact]
    public async Task Rejecting_changes_the_status_and_leaves_one_audit_row()
    {
        var (_, admin, loanId) = await ScenarioAsync();

        var response = await admin.RejectAsync(loanId);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.ReadJsonAsync()).GetProperty("status").GetString().Should().Be("Rejected");
        (await Factory.AuditRowsAsync(loanId)).Should().ContainSingle().Which.Decision.Should().Be(LoanStatus.Rejected);
    }

    [SqlServerFact]
    public async Task Deciding_twice_returns_409_and_changes_nothing()
    {
        var (_, admin, loanId) = await ScenarioAsync();
        (await admin.ApproveAsync(loanId)).StatusCode.Should().Be(HttpStatusCode.OK);

        var again = await admin.ApproveAsync(loanId);
        var reject = await admin.RejectAsync(loanId);

        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        again.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        reject.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Factory.StoredStatusAsync(loanId)).Should().Be(LoanStatus.Approved);
        (await Factory.AuditRowsAsync(loanId)).Should().ContainSingle();
    }

    [SqlServerFact]
    public async Task A_stale_version_in_the_body_returns_409()
    {
        var (_, admin, loanId) = await ScenarioAsync();

        var stale = await admin.ApproveAsync(loanId, new { version = 99 });
        var current = await admin.ApproveAsync(loanId, new { version = 1 });

        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
        current.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [SqlServerFact]
    public async Task Deciding_an_unknown_loan_returns_404()
    {
        var (_, admin, _) = await ScenarioAsync();

        (await admin.ApproveAsync(Guid.NewGuid())).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [SqlServerFact]
    public async Task The_status_the_borrower_reads_is_fresh_right_after_the_decision()
    {
        var (borrower, admin, loanId) = await ScenarioAsync();

        var before = await (await borrower.GetAsync($"/api/loans/{loanId}/status")).ReadJsonAsync();
        before.GetProperty("status").GetString().Should().Be("Pending");

        (await admin.ApproveAsync(loanId)).StatusCode.Should().Be(HttpStatusCode.OK);

        var after = await (await borrower.GetAsync($"/api/loans/{loanId}/status")).ReadJsonAsync();
        after.GetProperty("status").GetString().Should().Be("Approved");
        (await (await borrower.GetAsync("/api/loans/mine")).ReadJsonAsync()).EnumerateArray().Single()
            .GetProperty("status").GetString().Should().Be("Approved");
    }

    [SqlServerFact]
    public async Task An_admin_deletes_a_pending_loan_but_not_a_decided_one()
    {
        var (borrower, admin, pendingId) = await ScenarioAsync();
        var decidedId = await borrower.CreateLoanAsync(2000m, 6);
        await admin.RejectAsync(decidedId);

        (await admin.DeleteAsync($"/api/loans/{pendingId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await admin.DeleteAsync($"/api/loans/{decidedId}")).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [SqlServerFact]
    public async Task A_decided_loan_is_immutable_for_everyone()
    {
        var (borrower, admin, loanId) = await ScenarioAsync();
        await admin.ApproveAsync(loanId);

        (await borrower.DeleteAsync($"/api/loans/{loanId}")).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await admin.DeleteAsync($"/api/loans/{loanId}")).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Factory.StoredStatusAsync(loanId)).Should().Be(LoanStatus.Approved);
    }
}
