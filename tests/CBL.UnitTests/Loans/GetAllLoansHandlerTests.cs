using CBL.Domain.Loans;
using CBL.Domain.Users;
using CBL.Infrastructure.Persistence;
using CBL.UnitTests.Support;

namespace CBL.UnitTests.Loans;

public class GetAllLoansHandlerTests
{
    [Fact]
    public async Task Without_a_filter_returns_every_loan_with_borrower_name_and_email()
    {
        using var app = await TestApp.CreateAsync();
        var other = await app.AddUserAsync(Role.Borrower, "otro@test.com");
        await app.AddLoanAsync(SeedData.BorrowerId);
        await app.AddLoanAsync(other.Id);

        var all = await app.GetAllLoans.HandleAsync(null, default);

        all.Should().HaveCount(2);
        all.Select(l => l.BorrowerEmail).Should().BeEquivalentTo(SeedData.BorrowerEmail, "otro@test.com");
        all.Should().OnlyContain(l => l.BorrowerName != string.Empty);
    }

    [Fact]
    public async Task The_status_filter_returns_only_loans_in_that_status()
    {
        using var app = await TestApp.CreateAsync();
        await app.AddLoanAsync();
        var approved = await app.AddLoanAsync();
        var rejected = await app.AddLoanAsync();
        approved.Approve(SeedData.AdminId, app.Clock.UtcNow);
        rejected.Reject(SeedData.AdminId, app.Clock.UtcNow);
        await app.Db.SaveChangesAsync();

        (await app.GetAllLoans.HandleAsync(LoanStatus.Pending, default)).Should().ContainSingle();
        (await app.GetAllLoans.HandleAsync(LoanStatus.Approved, default)).Should().ContainSingle().Which.Id.Should().Be(approved.Id);
        (await app.GetAllLoans.HandleAsync(LoanStatus.Rejected, default)).Should().ContainSingle().Which.Id.Should().Be(rejected.Id);
        (await app.GetAllLoans.HandleAsync(null, default)).Should().HaveCount(3);
    }

    [Fact]
    public async Task Loans_come_newest_first()
    {
        using var app = await TestApp.CreateAsync();
        var oldest = await app.AddLoanAsync(createdAt: app.Clock.UtcNow.AddDays(-2));
        var newest = await app.AddLoanAsync(createdAt: app.Clock.UtcNow);
        var middle = await app.AddLoanAsync(createdAt: app.Clock.UtcNow.AddDays(-1));

        var all = await app.GetAllLoans.HandleAsync(null, default);

        all.Select(l => l.Id).Should().Equal(newest.Id, middle.Id, oldest.Id);
    }

    [Fact]
    public async Task The_list_is_not_paged()
    {
        using var app = await TestApp.CreateAsync();

        for (var i = 0; i < 75; i++)
        {
            await app.AddLoanAsync(createdAt: app.Clock.UtcNow.AddMinutes(-i));
        }

        (await app.GetAllLoans.HandleAsync(null, default)).Should().HaveCount(75);
    }
}
