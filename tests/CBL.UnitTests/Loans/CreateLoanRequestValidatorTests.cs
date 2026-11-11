using CBL.Application.Loans;

namespace CBL.UnitTests.Loans;

public class CreateLoanRequestValidatorTests
{
    private readonly CreateLoanRequestValidator _validator = new();

    [Theory]
    [InlineData(1000, 1)]
    [InlineData(5000.50, 60)]
    [InlineData(100000, 12)]
    public void Valid_requests_pass(double amount, int term) =>
        _validator.Validate(new CreateLoanRequest((decimal)amount, term)).IsValid.Should().BeTrue();

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    [InlineData(10.999)]
    public void Invalid_amounts_report_the_amount_field(double amount)
    {
        var result = _validator.Validate(new CreateLoanRequest((decimal)amount, 12));

        result.Errors.Should().ContainSingle().Which.PropertyName.Should().Be("Amount");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(61)]
    [InlineData(-3)]
    public void Invalid_terms_report_the_term_field(int term)
    {
        var result = _validator.Validate(new CreateLoanRequest(5000m, term));

        result.Errors.Should().ContainSingle().Which.PropertyName.Should().Be("TermMonths");
    }

    [Fact]
    public void Both_fields_can_fail_at_the_same_time()
    {
        var result = _validator.Validate(new CreateLoanRequest(0m, 0));

        result.Errors.Select(e => e.PropertyName).Should().BeEquivalentTo("Amount", "TermMonths");
    }
}
