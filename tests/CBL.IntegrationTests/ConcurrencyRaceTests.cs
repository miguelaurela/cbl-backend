using System.Net;
using CBL.Domain.Loans;
using CBL.IntegrationTests.Support;

namespace CBL.IntegrationTests;

/// <summary>
/// Carrera real: dos administradores deciden a la vez sobre el mismo préstamo. Solo se puede demostrar contra SQL Server
/// (transacción real y UPDATE condicionado por la versión), por eso solo corre en CI.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ConcurrencyRaceTests(DatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    [SqlServerFact(RequiresSqlServer = true)]
    public async Task Two_simultaneous_decisions_only_one_prevails_and_only_one_audit_row_exists()
    {
        var borrower = await Factory.CreateAuthorizedClientAsync(TestUsers.BorrowerEmail);
        var adminA = await Factory.CreateAuthorizedClientAsync(TestUsers.AdminEmail);
        var adminB = await Factory.CreateAuthorizedClientAsync(TestUsers.AdminEmail);
        var loanId = await borrower.CreateLoanAsync();

        using var gate = new ManualResetEventSlim(false);

        var approve = Task.Run(async () =>
        {
            gate.Wait();
            return await adminA.ApproveAsync(loanId);
        });

        var reject = Task.Run(async () =>
        {
            gate.Wait();
            return await adminB.RejectAsync(loanId);
        });

        gate.Set();
        var responses = await Task.WhenAll(approve, reject);

        responses.Select(r => r.StatusCode).Should().BeEquivalentTo(new[] { HttpStatusCode.OK, HttpStatusCode.Conflict });

        var winner = responses.Single(r => r.StatusCode == HttpStatusCode.OK);
        var winnerStatus = (await winner.ReadJsonAsync()).GetProperty("status").GetString();

        (await Factory.StoredStatusAsync(loanId)).ToString().Should().Be(winnerStatus);

        var audit = await Factory.AuditRowsAsync(loanId);
        audit.Should().ContainSingle("la decisión perdedora no deja rastro: el guardado es atómico");
        audit[0].Decision.ToString().Should().Be(winnerStatus);
    }

    [SqlServerFact(RequiresSqlServer = true)]
    public async Task Many_simultaneous_decisions_still_leave_exactly_one_winner()
    {
        var borrower = await Factory.CreateAuthorizedClientAsync(TestUsers.BorrowerEmail);
        var loanId = await borrower.CreateLoanAsync();
        var admins = new List<HttpClient>();

        for (var i = 0; i < 6; i++)
        {
            admins.Add(await Factory.CreateAuthorizedClientAsync(TestUsers.AdminEmail));
        }

        using var gate = new ManualResetEventSlim(false);
        var tasks = admins.Select((admin, i) => Task.Run(async () =>
        {
            gate.Wait();
            return i % 2 == 0 ? await admin.ApproveAsync(loanId) : await admin.RejectAsync(loanId);
        })).ToList();

        gate.Set();
        var responses = await Task.WhenAll(tasks);

        responses.Count(r => r.StatusCode == HttpStatusCode.OK).Should().Be(1);
        responses.Count(r => r.StatusCode == HttpStatusCode.Conflict).Should().Be(5);
        (await Factory.AuditRowsAsync(loanId)).Should().ContainSingle();
        (await Factory.StoredStatusAsync(loanId)).Should().NotBe(LoanStatus.Pending);
    }
}
