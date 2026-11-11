using CBL.Application.Abstractions;
using CBL.Application.Common.Exceptions;
using CBL.Application.Loans;
using CBL.Application.Parameters;
using CBL.Domain.Parameters;
using CBL.Domain.Users;
using CBL.Infrastructure.Persistence;
using CBL.UnitTests.Support;
using Microsoft.EntityFrameworkCore;

namespace CBL.UnitTests.Parameters;

public class LoanLimitsHandlersTests
{
    private readonly LoanLimitsValidator _validator = new();

    [Theory]
    [InlineData(1000, 100000, true)]
    [InlineData(0.01, 0.02, true)]
    [InlineData(0, 100, false)]
    [InlineData(-5, 100, false)]
    [InlineData(500, 500, false)]
    [InlineData(500, 100, false)]
    [InlineData(10.123, 500, false)]
    [InlineData(100, 500.555, false)]
    public void Limits_need_min_above_zero_and_max_above_min_with_two_decimals(double min, double max, bool valid) =>
        _validator.Validate(new LoanLimits((decimal)min, (decimal)max)).IsValid.Should().Be(valid);

    [Fact]
    public void Limits_above_the_ceiling_are_rejected() =>
        _validator.Validate(new LoanLimits(1000m, LoanLimitsValidator.Ceiling + 1)).IsValid.Should().BeFalse();

    [Fact]
    public void Errors_name_the_offending_field_in_camel_case()
    {
        var result = _validator.Validate(new LoanLimits(0m, 0m));

        ValidationFailedException.FromResult(result).Errors.Keys.Should().BeEquivalentTo("minAmount", "maxAmount");
    }

    [Fact]
    public async Task Reading_returns_the_seeded_defaults()
    {
        using var app = await TestApp.CreateAsync();

        (await app.GetLoanLimits.HandleAsync(default)).Should().Be(new LoanLimits(1000m, 100000m));
    }

    [Fact]
    public async Task Updating_persists_the_new_limits_who_and_when_and_invalidates_the_cache_after_saving()
    {
        using var app = await TestApp.CreateAsync();
        app.SignInAs(SeedData.AdminId, Role.Admin);
        await app.GetLoanLimits.HandleAsync(default);   // llena la caché
        app.Calls.Clear();
        app.Clock.Advance(TimeSpan.FromHours(1));

        var result = await app.UpdateLoanLimits.HandleAsync(new LoanLimits(2000m, 50000.50m), default);

        result.Should().Be(new LoanLimits(2000m, 50000.50m));
        app.Calls.Should().Equal("save", $"remove:{CacheKeys.LoanLimits}");

        var min = await app.Db.SystemParameters.SingleAsync(p => p.Key == SystemParameter.LoanAmountMin);
        var max = await app.Db.SystemParameters.SingleAsync(p => p.Key == SystemParameter.LoanAmountMax);
        min.Value.Should().Be("2000");
        max.Value.Should().Be("50000.50");
        min.UpdatedByUserId.Should().Be(SeedData.AdminId);
        min.UpdatedAt.Should().Be(app.Clock.UtcNow);

        (await app.GetLoanLimits.HandleAsync(default)).Should().Be(new LoanLimits(2000m, 50000.50m));
    }

    [Fact]
    public async Task If_the_save_fails_the_cache_is_left_untouched()
    {
        using var app = await TestApp.CreateAsync();
        app.SignInAs(SeedData.AdminId, Role.Admin);
        app.Uow.ThrowOnSave = new ConcurrencyConflictException("carrera");

        var act = () => app.UpdateLoanLimits.HandleAsync(new LoanLimits(2000m, 50000m), default);

        await act.Should().ThrowAsync<ConcurrencyConflictException>();
        app.Calls.Should().NotContain(c => c.StartsWith("remove:"));
    }

    [Fact]
    public async Task The_next_request_uses_the_new_limits_and_loans_already_registered_keep_their_amount()
    {
        using var app = await TestApp.CreateAsync();

        app.SignInAs(SeedData.BorrowerId, Role.Borrower);
        var before = await app.RequestLoan.HandleAsync(new CreateLoanRequest(1500m, 12), default);

        app.SignInAs(SeedData.AdminId, Role.Admin);
        await app.UpdateLoanLimits.HandleAsync(new LoanLimits(2000m, 50000m), default);

        app.SignInAs(SeedData.BorrowerId, Role.Borrower);
        var tooLow = () => app.RequestLoan.HandleAsync(new CreateLoanRequest(1500m, 12), default);
        var tooHigh = () => app.RequestLoan.HandleAsync(new CreateLoanRequest(60000m, 12), default);
        var inside = await app.RequestLoan.HandleAsync(new CreateLoanRequest(2000m, 12), default);

        await tooLow.Should().ThrowAsync<ValidationFailedException>();
        await tooHigh.Should().ThrowAsync<ValidationFailedException>();
        inside.Amount.Should().Be(2000m);
        (await app.Db.Loans.SingleAsync(l => l.Id == before.Id)).Amount.Should().Be(1500m, "las solicitudes ya registradas no cambian");
    }

    [Fact]
    public async Task Missing_parameters_are_created_on_update()
    {
        using var app = await TestApp.CreateAsync();
        app.SignInAs(SeedData.AdminId, Role.Admin);
        app.Db.SystemParameters.RemoveRange(app.Db.SystemParameters);
        await app.Db.SaveChangesAsync();

        await app.UpdateLoanLimits.HandleAsync(new LoanLimits(3000m, 9000m), default);

        (await app.Db.SystemParameters.CountAsync()).Should().Be(2);
    }
}
