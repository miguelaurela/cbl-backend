using CBL.Application.Abstractions;
using CBL.Application.Common.Exceptions;
using CBL.Domain.Users;
using CBL.Infrastructure.Persistence;
using CBL.UnitTests.Support;

namespace CBL.UnitTests.Loans;

public class LoanQueriesTests
{
    [Fact]
    public async Task My_loans_returns_only_the_loans_of_the_authenticated_borrower()
    {
        using var app = await TestApp.CreateAsync();
        var other = await app.AddUserAsync(Role.Borrower, "otro@test.com");
        await app.AddLoanAsync(SeedData.BorrowerId, 1500m);
        await app.AddLoanAsync(SeedData.BorrowerId, 2500m);
        await app.AddLoanAsync(other.Id, 9000m);

        app.SignInAs(SeedData.BorrowerId, Role.Borrower);
        var mine = await app.GetMyLoans.HandleAsync(default);

        mine.Should().HaveCount(2);
        mine.Should().OnlyContain(l => l.BorrowerId == SeedData.BorrowerId && l.BorrowerEmail == SeedData.BorrowerEmail);
    }

    [Fact]
    public async Task Loan_by_id_is_visible_to_its_owner_and_to_any_admin()
    {
        using var app = await TestApp.CreateAsync();
        var loan = await app.AddLoanAsync(SeedData.BorrowerId);

        app.SignInAs(SeedData.BorrowerId, Role.Borrower);
        (await app.GetLoanById.HandleAsync(loan.Id, default)).Id.Should().Be(loan.Id);

        app.SignInAs(SeedData.AdminId, Role.Admin);
        (await app.GetLoanById.HandleAsync(loan.Id, default)).Id.Should().Be(loan.Id);
    }

    [Fact]
    public async Task Loan_by_id_of_another_borrower_looks_nonexistent_to_avoid_enumeration()
    {
        using var app = await TestApp.CreateAsync();
        var intruder = await app.AddUserAsync(Role.Borrower);
        var loan = await app.AddLoanAsync(SeedData.BorrowerId);

        app.SignInAs(intruder);
        var act = () => app.GetLoanById.HandleAsync(loan.Id, default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Status_is_returned_to_the_owner()
    {
        using var app = await TestApp.CreateAsync();
        var loan = await app.AddLoanAsync(SeedData.BorrowerId);

        app.SignInAs(SeedData.BorrowerId, Role.Borrower);
        var status = await app.GetLoanStatus.HandleAsync(loan.Id, default);

        status.Status.Should().Be("Pending");
        status.Id.Should().Be(loan.Id);
    }

    [Fact]
    public async Task Status_with_a_cache_hit_does_not_touch_the_repository()
    {
        using var app = await TestApp.CreateAsync();
        var loan = await app.AddLoanAsync(SeedData.BorrowerId);
        app.SignInAs(SeedData.BorrowerId, Role.Borrower);

        await app.GetLoanStatus.HandleAsync(loan.Id, default);
        app.Cache.Contains(CacheKeys.LoanStatus(loan.Id)).Should().BeTrue();

        // Si la segunda lectura fuese a la base, fallaría con NotFound: el préstamo ya no existe.
        app.Db.Loans.Remove(loan);
        await app.Db.SaveChangesAsync();

        var cached = await app.GetLoanStatus.HandleAsync(loan.Id, default);

        cached.Status.Should().Be("Pending");
    }

    [Fact]
    public async Task Authorization_is_checked_before_answering_from_the_cache()
    {
        using var app = await TestApp.CreateAsync();
        var intruder = await app.AddUserAsync(Role.Borrower);
        var loan = await app.AddLoanAsync(SeedData.BorrowerId);

        app.SignInAs(SeedData.BorrowerId, Role.Borrower);
        await app.GetLoanStatus.HandleAsync(loan.Id, default);   // llena la caché con el estado del dueño

        app.SignInAs(intruder);
        var act = () => app.GetLoanStatus.HandleAsync(loan.Id, default);

        await act.Should().ThrowAsync<NotFoundException>("el prestamista B no puede leer el estado cacheado del A");
    }

    [Fact]
    public async Task An_admin_can_read_the_status_of_any_loan()
    {
        using var app = await TestApp.CreateAsync();
        var loan = await app.AddLoanAsync(SeedData.BorrowerId);

        app.SignInAs(SeedData.AdminId, Role.Admin);

        (await app.GetLoanStatus.HandleAsync(loan.Id, default)).Status.Should().Be("Pending");
    }

    [Fact]
    public async Task Status_of_an_unknown_loan_is_not_found()
    {
        using var app = await TestApp.CreateAsync();
        app.SignInAs(SeedData.BorrowerId, Role.Borrower);

        var act = () => app.GetLoanStatus.HandleAsync(Guid.NewGuid(), default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task After_a_decision_the_cached_status_is_refreshed_and_never_stale()
    {
        using var app = await TestApp.CreateAsync();
        var loan = await app.AddLoanAsync(SeedData.BorrowerId);

        app.SignInAs(SeedData.BorrowerId, Role.Borrower);
        (await app.GetLoanStatus.HandleAsync(loan.Id, default)).Status.Should().Be("Pending");

        app.SignInAs(SeedData.AdminId, Role.Admin);
        await app.ApproveLoan.HandleAsync(loan.Id, null, default);

        app.SignInAs(SeedData.BorrowerId, Role.Borrower);
        var after = await app.GetLoanStatus.HandleAsync(loan.Id, default);

        after.Status.Should().Be("Approved");
        after.DecidedAt.Should().NotBeNull();
    }
}
