using System.Diagnostics;
using System.Net;
using CBL.IntegrationTests.Support;

namespace CBL.IntegrationTests;

/// <summary>SC-006: el 95 % de las consultas de estado de préstamos responde en menos de 1 segundo; y nunca con datos viejos.</summary>
[Trait("Category", "Integration")]
public sealed class StatusQueryPerformanceTests(DatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    [SqlServerFact]
    public async Task Ninety_five_percent_of_repeated_status_queries_answer_in_under_one_second()
    {
        var borrower = await Factory.CreateAuthorizedClientAsync(TestUsers.BorrowerEmail);
        var loanId = await borrower.CreateLoanAsync();
        (await borrower.GetAsync($"/api/loans/{loanId}/status")).StatusCode.Should().Be(HttpStatusCode.OK);   // calentamiento

        var timings = new List<double>();

        for (var i = 0; i < 100; i++)
        {
            var timer = Stopwatch.StartNew();
            var response = await borrower.GetAsync($"/api/loans/{loanId}/status");
            timer.Stop();

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            timings.Add(timer.Elapsed.TotalMilliseconds);
        }

        timings.Sort();
        var p95 = timings[(int)Math.Ceiling(timings.Count * 0.95) - 1];

        p95.Should().BeLessThan(1000, "SC-006: p95 de las consultas de estado en menos de 1 s (medido: {0:F1} ms)", p95);
    }

    [SqlServerFact]
    public async Task The_status_read_after_a_decision_is_the_new_one_even_after_many_cached_reads()
    {
        var borrower = await Factory.CreateAuthorizedClientAsync(TestUsers.BorrowerEmail);
        var admin = await Factory.CreateAuthorizedClientAsync(TestUsers.AdminEmail);
        var loanId = await borrower.CreateLoanAsync();

        for (var i = 0; i < 20; i++)
        {
            (await (await borrower.GetAsync($"/api/loans/{loanId}/status")).ReadJsonAsync()).GetProperty("status").GetString()
                .Should().Be("Pending");
        }

        (await admin.ApproveAsync(loanId)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await (await borrower.GetAsync($"/api/loans/{loanId}/status")).ReadJsonAsync()).GetProperty("status").GetString()
            .Should().Be("Approved");
    }
}
