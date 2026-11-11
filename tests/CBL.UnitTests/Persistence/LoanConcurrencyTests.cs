using CBL.Application.Common.Exceptions;
using CBL.Domain.Loans;
using CBL.Infrastructure.Persistence;
using CBL.UnitTests.Support;
using Microsoft.EntityFrameworkCore;

namespace CBL.UnitTests.Persistence;

/// <summary>
/// Demuestra que el token <c>Version</c> funciona también con EF InMemory (no depende de rowversion): dos contextos
/// sobre el mismo almacén y el mismo préstamo; la segunda escritura falla (constitución II, ADR-0007).
/// </summary>
public class LoanConcurrencyTests
{
    private static async Task<(string Store, Guid LoanId)> SeedLoanAsync()
    {
        var store = TestDb.NewStoreName();
        await using var seed = TestDb.Create(store);
        await InMemorySeeder.SeedAsync(seed, new FakeClock());

        var loan = Builders.PendingLoan(SeedData.BorrowerId);
        seed.Loans.Add(loan);
        await seed.SaveChangesAsync();

        return (store, loan.Id);
    }

    [Fact]
    public async Task The_second_save_of_a_stale_loan_throws_a_concurrency_exception()
    {
        var (store, loanId) = await SeedLoanAsync();
        await using var first = TestDb.Create(store);
        await using var second = TestDb.Create(store);

        var loanInFirst = await first.Loans.SingleAsync(l => l.Id == loanId);
        var loanInSecond = await second.Loans.SingleAsync(l => l.Id == loanId);

        loanInFirst.Approve(SeedData.AdminId, Builders.Now);
        await first.SaveChangesAsync();

        loanInSecond.Reject(SeedData.AdminId, Builders.Now);
        var act = () => second.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
        (await TestDb.Create(store).Loans.SingleAsync()).Status.Should().Be(LoanStatus.Approved);
    }

    [Fact]
    public async Task The_unit_of_work_translates_the_conflict_to_the_application_exception_that_becomes_409()
    {
        var (store, loanId) = await SeedLoanAsync();
        await using var first = TestDb.Create(store);
        await using var second = TestDb.Create(store);

        var loanInFirst = await first.Loans.SingleAsync(l => l.Id == loanId);
        var loanInSecond = await second.Loans.SingleAsync(l => l.Id == loanId);

        loanInFirst.Approve(SeedData.AdminId, Builders.Now);
        await new UnitOfWork(first).SaveChangesAsync(default);

        loanInSecond.Approve(SeedData.AdminId, Builders.Now);
        var act = () => new UnitOfWork(second).SaveChangesAsync(default);

        await act.Should().ThrowAsync<ConcurrencyConflictException>();
    }
}
