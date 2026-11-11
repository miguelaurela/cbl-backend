using CBL.Application.Abstractions;
using CBL.Domain.Loans;
using CBL.Domain.Parameters;
using CBL.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace CBL.Infrastructure.Persistence.Repositories;

internal sealed class UserRepository(CblDbContext db) : IUserRepository
{
    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct) =>
        db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken ct) =>
        db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct);

    public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken ct) =>
        db.Users.AnyAsync(u => u.Email == normalizedEmail, ct);

    public async Task<IReadOnlyList<User>> ListAsync(CancellationToken ct) =>
        await db.Users.OrderBy(u => u.Email).ToListAsync(ct);

    public async Task AddAsync(User user, CancellationToken ct) => await db.Users.AddAsync(user, ct);
}

internal sealed class LoanRepository(CblDbContext db) : ILoanRepository
{
    public Task<Loan?> GetByIdAsync(Guid id, CancellationToken ct) =>
        db.Loans
            .Include(l => l.Borrower)
            .Include(l => l.DecidedBy)
            .FirstOrDefaultAsync(l => l.Id == id, ct);

    public async Task<IReadOnlyList<Loan>> ListByBorrowerAsync(Guid borrowerId, CancellationToken ct) =>
        await db.Loans
            .Include(l => l.Borrower)
            .Include(l => l.DecidedBy)
            .Where(l => l.BorrowerId == borrowerId)
            .OrderByDescending(l => l.CreatedAt)
            .ThenBy(l => l.Id)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Loan>> ListAsync(LoanStatus? status, CancellationToken ct)
    {
        var query = db.Loans.Include(l => l.Borrower).Include(l => l.DecidedBy).AsQueryable();

        if (status is { } s)
        {
            query = query.Where(l => l.Status == s);
        }

        return await query.OrderByDescending(l => l.CreatedAt).ThenBy(l => l.Id).ToListAsync(ct);
    }

    public async Task AddAsync(Loan loan, CancellationToken ct) => await db.Loans.AddAsync(loan, ct);

    public void Remove(Loan loan) => db.Loans.Remove(loan);
}

internal sealed class SystemParameterRepository(CblDbContext db) : ISystemParameterRepository
{
    public Task<SystemParameter?> GetAsync(string key, CancellationToken ct) =>
        db.SystemParameters.FirstOrDefaultAsync(p => p.Key == key, ct);

    public async Task AddAsync(SystemParameter parameter, CancellationToken ct) =>
        await db.SystemParameters.AddAsync(parameter, ct);
}

internal sealed class LoanAuditRepository(CblDbContext db) : ILoanAuditRepository
{
    public async Task AddAsync(LoanDecisionAudit audit, CancellationToken ct) =>
        await db.LoanDecisionAudits.AddAsync(audit, ct);
}
