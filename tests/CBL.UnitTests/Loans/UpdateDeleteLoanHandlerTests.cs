using CBL.Application.Abstractions;
using CBL.Application.Common.Exceptions;
using CBL.Application.Loans;
using CBL.Domain.Exceptions;
using CBL.Domain.Loans;
using CBL.Domain.Users;
using CBL.Infrastructure.Persistence;
using CBL.UnitTests.Support;
using Microsoft.EntityFrameworkCore;

namespace CBL.UnitTests.Loans;

public class UpdateDeleteLoanHandlerTests
{
    private static async Task Decide(TestApp app, Loan loan)
    {
        loan.Approve(SeedData.AdminId, app.Clock.UtcNow);
        await app.Db.SaveChangesAsync();
    }

    [Fact]
    public async Task The_owner_edits_a_pending_loan_and_the_status_cache_is_invalidated()
    {
        using var app = await TestApp.CreateAsync();
        var loan = await app.AddLoanAsync(SeedData.BorrowerId, 5000m, 12);
        app.SignInAs(SeedData.BorrowerId, Role.Borrower);

        var dto = await app.UpdateLoan.HandleAsync(loan.Id, new CreateLoanRequest(7500m, 24), default);

        dto.Amount.Should().Be(7500m);
        dto.TermMonths.Should().Be(24);
        dto.Version.Should().Be(2);
        dto.Status.Should().Be("Pending");
        app.Calls.Should().Equal("save", $"remove:{CacheKeys.LoanStatus(loan.Id)}");
    }

    [Fact]
    public async Task Editing_revalidates_the_amount_against_the_current_limits()
    {
        using var app = await TestApp.CreateAsync();
        var loan = await app.AddLoanAsync(SeedData.BorrowerId);
        app.SignInAs(SeedData.BorrowerId, Role.Borrower);

        var tooBig = () => app.UpdateLoan.HandleAsync(loan.Id, new CreateLoanRequest(100001m, 12), default);
        var badTerm = () => app.UpdateLoan.HandleAsync(loan.Id, new CreateLoanRequest(5000m, 61), default);

        (await tooBig.Should().ThrowAsync<ValidationFailedException>()).Which.Errors.Should().ContainKey("amount");
        (await badTerm.Should().ThrowAsync<DomainValidationException>()).Which.Field.Should().Be("termMonths");
    }

    [Fact]
    public async Task A_decided_loan_cannot_be_edited_even_by_its_owner()
    {
        using var app = await TestApp.CreateAsync();
        var loan = await app.AddLoanAsync(SeedData.BorrowerId);
        await Decide(app, loan);
        app.SignInAs(SeedData.BorrowerId, Role.Borrower);

        var act = () => app.UpdateLoan.HandleAsync(loan.Id, new CreateLoanRequest(7500m, 24), default);

        await act.Should().ThrowAsync<LoanNotModifiableException>();
    }

    [Fact]
    public async Task A_decided_loan_reports_conflict_before_the_validation_of_the_new_values()
    {
        using var app = await TestApp.CreateAsync();
        var loan = await app.AddLoanAsync(SeedData.BorrowerId);
        await Decide(app, loan);
        app.SignInAs(SeedData.BorrowerId, Role.Borrower);

        var act = () => app.UpdateLoan.HandleAsync(loan.Id, new CreateLoanRequest(999_999m, 24), default);

        await act.Should().ThrowAsync<LoanNotModifiableException>();
    }

    [Fact]
    public async Task Another_borrower_cannot_edit_or_delete_the_loan_it_looks_nonexistent()
    {
        using var app = await TestApp.CreateAsync();
        var intruder = await app.AddUserAsync(Role.Borrower);
        var loan = await app.AddLoanAsync(SeedData.BorrowerId);
        app.SignInAs(intruder);

        var update = () => app.UpdateLoan.HandleAsync(loan.Id, new CreateLoanRequest(7500m, 24), default);
        var delete = () => app.DeleteLoan.HandleAsync(loan.Id, default);

        await update.Should().ThrowAsync<NotFoundException>();
        await delete.Should().ThrowAsync<NotFoundException>();
        (await app.Db.Loans.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task The_owner_deletes_a_pending_loan()
    {
        using var app = await TestApp.CreateAsync();
        var loan = await app.AddLoanAsync(SeedData.BorrowerId);
        app.SignInAs(SeedData.BorrowerId, Role.Borrower);

        await app.DeleteLoan.HandleAsync(loan.Id, default);

        (await app.Db.Loans.CountAsync()).Should().Be(0);
        app.Calls.Should().Contain($"remove:{CacheKeys.LoanStatus(loan.Id)}");
    }

    [Fact]
    public async Task A_decided_loan_cannot_be_deleted_by_its_owner()
    {
        using var app = await TestApp.CreateAsync();
        var loan = await app.AddLoanAsync(SeedData.BorrowerId);
        await Decide(app, loan);
        app.SignInAs(SeedData.BorrowerId, Role.Borrower);

        var act = () => app.DeleteLoan.HandleAsync(loan.Id, default);

        await act.Should().ThrowAsync<LoanNotModifiableException>();
        (await app.Db.Loans.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task An_admin_deletes_only_pending_loans()
    {
        using var app = await TestApp.CreateAsync();
        var pending = await app.AddLoanAsync(SeedData.BorrowerId);
        var decided = await app.AddLoanAsync(SeedData.BorrowerId);
        await Decide(app, decided);
        app.SignInAs(SeedData.AdminId, Role.Admin);

        await app.DeleteLoan.HandleAsync(pending.Id, default);
        var act = () => app.DeleteLoan.HandleAsync(decided.Id, default);

        await act.Should().ThrowAsync<LoanNotModifiableException>();
        (await app.Db.Loans.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Deleting_an_unknown_loan_is_not_found()
    {
        using var app = await TestApp.CreateAsync();
        app.SignInAs(SeedData.AdminId, Role.Admin);

        var act = () => app.DeleteLoan.HandleAsync(Guid.NewGuid(), default);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
