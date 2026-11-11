using CBL.Application.Abstractions;
using CBL.Application.Common.Exceptions;
using CBL.Domain.Exceptions;
using CBL.Domain.Loans;
using CBL.Domain.Users;
using CBL.Infrastructure.Persistence;
using CBL.UnitTests.Support;
using Microsoft.EntityFrameworkCore;

namespace CBL.UnitTests.Loans;

public class DecideLoanHandlerTests
{
    private static async Task<(TestApp App, Loan Loan)> PendingLoanAsync()
    {
        var app = await TestApp.CreateAsync();
        var loan = await app.AddLoanAsync(SeedData.BorrowerId);
        app.SignInAs(SeedData.AdminId, Role.Admin);
        app.Calls.Clear();

        return (app, loan);
    }

    [Fact]
    public async Task Approving_records_the_admin_and_the_time_in_one_audit_row_and_one_save()
    {
        var (app, loan) = await PendingLoanAsync();
        using var _ = app;
        app.Clock.Advance(TimeSpan.FromHours(2));

        var dto = await app.ApproveLoan.HandleAsync(loan.Id, null, default);

        dto.Status.Should().Be("Approved");
        dto.DecidedById.Should().Be(SeedData.AdminId);
        dto.DecidedByName.Should().Be("Administrador de prueba");
        dto.DecidedAt.Should().Be(app.Clock.UtcNow);
        dto.Version.Should().Be(2);

        var audit = await app.Db.LoanDecisionAudits.SingleAsync();
        audit.LoanId.Should().Be(loan.Id);
        audit.AdminUserId.Should().Be(SeedData.AdminId);
        audit.Decision.Should().Be(LoanStatus.Approved);
        audit.PreviousStatus.Should().Be(LoanStatus.Pending);
        audit.DecidedAt.Should().Be(app.Clock.UtcNow);
        audit.CorrelationId.Should().Be("corr-123");

        app.Uow.SaveCount.Should().Be(1, "préstamo y auditoría se guardan juntos, en un único SaveChanges");
    }

    [Fact]
    public async Task Rejecting_records_a_rejected_audit_row()
    {
        var (app, loan) = await PendingLoanAsync();
        using var _ = app;

        var dto = await app.RejectLoan.HandleAsync(loan.Id, null, default);

        dto.Status.Should().Be("Rejected");
        (await app.Db.LoanDecisionAudits.SingleAsync()).Decision.Should().Be(LoanStatus.Rejected);
    }

    [Fact]
    public async Task A_second_decision_conflicts_and_adds_no_audit_row()
    {
        var (app, loan) = await PendingLoanAsync();
        using var _ = app;
        await app.ApproveLoan.HandleAsync(loan.Id, null, default);

        var approveAgain = () => app.ApproveLoan.HandleAsync(loan.Id, null, default);
        var rejectAfter = () => app.RejectLoan.HandleAsync(loan.Id, null, default);

        await approveAgain.Should().ThrowAsync<InvalidLoanTransitionException>();
        await rejectAfter.Should().ThrowAsync<InvalidLoanTransitionException>();
        (await app.Db.LoanDecisionAudits.CountAsync()).Should().Be(1);
        (await app.Db.Loans.SingleAsync()).Status.Should().Be(LoanStatus.Approved);
        app.Uow.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task The_cache_is_invalidated_only_after_the_save_is_confirmed()
    {
        var (app, loan) = await PendingLoanAsync();
        using var _ = app;

        await app.ApproveLoan.HandleAsync(loan.Id, null, default);

        app.Calls.Should().Equal("save", $"remove:{CacheKeys.LoanStatus(loan.Id)}");
    }

    [Fact]
    public async Task If_the_save_fails_the_cache_is_not_invalidated()
    {
        var (app, loan) = await PendingLoanAsync();
        using var _ = app;
        app.Uow.ThrowOnSave = new ConcurrencyConflictException("carrera");

        var act = () => app.ApproveLoan.HandleAsync(loan.Id, null, default);

        await act.Should().ThrowAsync<ConcurrencyConflictException>();
        app.Calls.Should().NotContain(c => c.StartsWith("remove:"));
    }

    [Fact]
    public async Task A_version_that_does_not_match_conflicts_before_changing_anything()
    {
        var (app, loan) = await PendingLoanAsync();
        using var _ = app;

        var act = () => app.ApproveLoan.HandleAsync(loan.Id, 99, default);

        await act.Should().ThrowAsync<ConcurrencyConflictException>();
        (await app.Db.Loans.SingleAsync()).Status.Should().Be(LoanStatus.Pending);
        (await app.Db.LoanDecisionAudits.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task A_matching_version_is_accepted()
    {
        var (app, loan) = await PendingLoanAsync();
        using var _ = app;

        var dto = await app.ApproveLoan.HandleAsync(loan.Id, loan.Version, default);

        dto.Status.Should().Be("Approved");
    }

    [Fact]
    public async Task Deciding_an_unknown_loan_is_not_found()
    {
        var (app, _) = await PendingLoanAsync();
        using var __ = app;

        var act = () => app.ApproveLoan.HandleAsync(Guid.NewGuid(), null, default);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
