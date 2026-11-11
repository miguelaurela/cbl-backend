using CBL.Domain.Exceptions;
using CBL.Domain.Loans;
using CBL.UnitTests.Support;

namespace CBL.UnitTests.Domain;

public class LoanDecisionTests
{
    private static readonly Guid Admin = Guid.NewGuid();
    private static readonly DateTime DecisionTime = Builders.Now.AddHours(3);

    [Fact]
    public void Approve_from_pending_records_who_and_when_and_bumps_the_version()
    {
        var loan = Builders.PendingLoan();

        loan.Approve(Admin, DecisionTime);

        loan.Status.Should().Be(LoanStatus.Approved);
        loan.DecidedByUserId.Should().Be(Admin);
        loan.DecidedAt.Should().Be(DecisionTime);
        loan.Version.Should().Be(2);
    }

    [Fact]
    public void Reject_from_pending_records_who_and_when_and_bumps_the_version()
    {
        var loan = Builders.PendingLoan();

        loan.Reject(Admin, DecisionTime);

        loan.Status.Should().Be(LoanStatus.Rejected);
        loan.DecidedByUserId.Should().Be(Admin);
        loan.DecidedAt.Should().Be(DecisionTime);
        loan.Version.Should().Be(2);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_second_decision_is_always_an_invalid_transition_and_changes_nothing(bool firstApprove)
    {
        var loan = Builders.PendingLoan();
        if (firstApprove)
        {
            loan.Approve(Admin, DecisionTime);
        }
        else
        {
            loan.Reject(Admin, DecisionTime);
        }

        var versionAfterFirst = loan.Version;
        var statusAfterFirst = loan.Status;

        var approveAgain = () => loan.Approve(Guid.NewGuid(), DecisionTime.AddHours(1));
        var rejectAgain = () => loan.Reject(Guid.NewGuid(), DecisionTime.AddHours(1));

        approveAgain.Should().Throw<InvalidLoanTransitionException>();
        rejectAgain.Should().Throw<InvalidLoanTransitionException>();
        loan.Status.Should().Be(statusAfterFirst);
        loan.Version.Should().Be(versionAfterFirst);
        loan.DecidedByUserId.Should().Be(Admin);
    }

    [Fact]
    public void A_decided_loan_cannot_be_edited_or_deleted()
    {
        var loan = Builders.PendingLoan();
        loan.Approve(Admin, DecisionTime);

        var update = () => loan.UpdateTerms(2000m, 6);
        var delete = () => loan.EnsureCanDelete();

        update.Should().Throw<LoanNotModifiableException>();
        delete.Should().Throw<LoanNotModifiableException>();
        loan.Amount.Should().Be(5000m);
    }

    [Fact]
    public void Editing_a_pending_loan_bumps_the_version_and_keeps_it_pending()
    {
        var loan = Builders.PendingLoan();

        loan.UpdateTerms(2000m, 6);

        loan.Amount.Should().Be(2000m);
        loan.TermMonths.Should().Be(6);
        loan.Version.Should().Be(2);
        loan.Status.Should().Be(LoanStatus.Pending);
    }
}
