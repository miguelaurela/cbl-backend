using CBL.Application.Abstractions;
using CBL.Application.Common.Exceptions;
using CBL.Application.Loans;
using CBL.Domain.Exceptions;
using CBL.Domain.Loans;
using CBL.Domain.Parameters;
using CBL.Domain.Users;
using CBL.Infrastructure.Persistence;
using CBL.UnitTests.Support;
using Microsoft.EntityFrameworkCore;

namespace CBL.UnitTests.Loans;

public class RequestLoanHandlerTests
{
    private static async Task<TestApp> BorrowerAppAsync()
    {
        var app = await TestApp.CreateAsync();
        app.SignInAs(SeedData.BorrowerId, Role.Borrower);

        return app;
    }

    [Fact]
    public async Task Creates_a_pending_loan_for_the_user_in_the_token()
    {
        using var app = await BorrowerAppAsync();

        var dto = await app.RequestLoan.HandleAsync(new CreateLoanRequest(5000m, 12), default);

        dto.Status.Should().Be("Pending");
        dto.BorrowerId.Should().Be(SeedData.BorrowerId);
        dto.BorrowerEmail.Should().Be(SeedData.BorrowerEmail);
        dto.Amount.Should().Be(5000m);
        dto.TermMonths.Should().Be(12);
        dto.Version.Should().Be(1);
        dto.DecidedAt.Should().BeNull();
        (await app.Db.Loans.CountAsync()).Should().Be(1);
    }

    [Theory]
    [InlineData(1000)]
    [InlineData(100000)]
    public async Task Amounts_equal_to_the_minimum_or_the_maximum_are_accepted(double amount)
    {
        using var app = await BorrowerAppAsync();

        var dto = await app.RequestLoan.HandleAsync(new CreateLoanRequest((decimal)amount, 6), default);

        dto.Amount.Should().Be((decimal)amount);
    }

    [Theory]
    [InlineData(999.99)]
    [InlineData(100000.01)]
    public async Task Amounts_outside_the_limits_are_rejected_on_the_amount_field(double amount)
    {
        using var app = await BorrowerAppAsync();

        var act = () => app.RequestLoan.HandleAsync(new CreateLoanRequest((decimal)amount, 6), default);

        (await act.Should().ThrowAsync<ValidationFailedException>()).Which.Errors.Should().ContainKey("amount");
        (await app.Db.Loans.CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(61)]
    public async Task Invalid_terms_are_rejected_on_the_term_field(int term)
    {
        using var app = await BorrowerAppAsync();

        var act = () => app.RequestLoan.HandleAsync(new CreateLoanRequest(5000m, term), default);

        (await act.Should().ThrowAsync<DomainValidationException>()).Which.Field.Should().Be("termMonths");
    }

    [Fact]
    public async Task Limits_come_from_the_parameters_through_the_read_cache()
    {
        using var app = await BorrowerAppAsync();
        await app.RequestLoan.HandleAsync(new CreateLoanRequest(5000m, 12), default);
        app.Cache.Contains(CacheKeys.LoanLimits).Should().BeTrue("la primera lectura llena la caché");

        // Cambiar el parámetro en base de datos no se nota mientras la caché siga vigente...
        var min = await app.Db.SystemParameters.SingleAsync(p => p.Key == SystemParameter.LoanAmountMin);
        min.Update("20000", app.Clock.UtcNow, SeedData.AdminId);
        await app.Db.SaveChangesAsync();

        await app.RequestLoan.HandleAsync(new CreateLoanRequest(5000m, 12), default);

        // ...pero tras invalidar la clave (lo que hará UpdateLoanLimits), el valor nuevo se aplica.
        app.Cache.Remove(CacheKeys.LoanLimits);
        var act = () => app.RequestLoan.HandleAsync(new CreateLoanRequest(5000m, 12), default);

        (await act.Should().ThrowAsync<ValidationFailedException>()).Which.Errors.Should().ContainKey("amount");
    }

    [Fact]
    public async Task Missing_limit_parameters_are_a_configuration_error_not_a_silent_default()
    {
        using var app = await BorrowerAppAsync();
        app.Db.SystemParameters.RemoveRange(app.Db.SystemParameters);
        await app.Db.SaveChangesAsync();

        var act = () => app.RequestLoan.HandleAsync(new CreateLoanRequest(5000m, 12), default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*LoanAmountMin*");
    }
}
