using CBL.Domain.Exceptions;
using CBL.Domain.Loans;
using CBL.UnitTests.Support;

namespace CBL.UnitTests.Domain;

public class LoanRequestTests
{
    [Fact]
    public void A_valid_request_is_born_pending_with_version_one()
    {
        var borrower = Guid.NewGuid();

        var loan = Loan.Request(borrower, 5000m, 12, Builders.Now);

        loan.Status.Should().Be(LoanStatus.Pending);
        loan.Version.Should().Be(1);
        loan.BorrowerId.Should().Be(borrower);
        loan.Amount.Should().Be(5000m);
        loan.TermMonths.Should().Be(12);
        loan.CreatedAt.Should().Be(Builders.Now);
        loan.DecidedAt.Should().BeNull();
        loan.DecidedByUserId.Should().BeNull();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(60)]
    public void Term_boundaries_one_and_sixty_are_accepted(int term) =>
        Loan.Request(Guid.NewGuid(), 1000m, term, Builders.Now).TermMonths.Should().Be(term);

    [Theory]
    [InlineData(0)]
    [InlineData(61)]
    [InlineData(-1)]
    public void Term_outside_one_to_sixty_is_rejected_on_the_term_field(int term)
    {
        var act = () => Loan.Request(Guid.NewGuid(), 1000m, term, Builders.Now);

        act.Should().Throw<DomainValidationException>().Which.Field.Should().Be("termMonths");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(100.123)]
    public void Amount_must_be_positive_with_at_most_two_decimals(double amount)
    {
        var act = () => Loan.Request(Guid.NewGuid(), (decimal)amount, 12, Builders.Now);

        act.Should().Throw<DomainValidationException>().Which.Field.Should().Be("amount");
    }

    [Fact]
    public void An_amount_with_two_decimals_is_accepted() =>
        Loan.Request(Guid.NewGuid(), 100.12m, 12, Builders.Now).Amount.Should().Be(100.12m);
}
