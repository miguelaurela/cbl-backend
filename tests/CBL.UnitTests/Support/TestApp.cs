using CBL.Application.Abstractions;
using CBL.Application.Loans.Commands;
using CBL.Application.Loans.Queries;
using CBL.Application.Parameters;
using CBL.Application.Users;
using CBL.Infrastructure.Security;
using CBL.Domain.Loans;
using CBL.Domain.Users;
using CBL.Infrastructure.Persistence;
using CBL.Infrastructure.Persistence.Repositories;

namespace CBL.UnitTests.Support;

/// <summary>Caché de lectura en memoria que además registra las llamadas (para comprobar el ORDEN de la invalidación).</summary>
public sealed class RecordingReadCache(List<string> calls) : IReadCache
{
    private readonly Dictionary<string, object?> _values = [];

    public bool TryGet<T>(string key, out T? value)
    {
        if (_values.TryGetValue(key, out var stored) && stored is T typed)
        {
            value = typed;
            return true;
        }

        value = default;
        return false;
    }

    public void Set<T>(string key, T value) => _values[key] = value;

    public void Remove(string key)
    {
        calls.Add($"remove:{key}");
        _values.Remove(key);
    }

    public bool Contains(string key) => _values.ContainsKey(key);
}

/// <summary>Unidad de trabajo real que cuenta los guardados, registra el orden y puede fallar a propósito.</summary>
public sealed class CountingUnitOfWork(IUnitOfWork inner, List<string> calls) : IUnitOfWork
{
    public int SaveCount { get; private set; }

    public Exception? ThrowOnSave { get; set; }

    public async Task SaveChangesAsync(CancellationToken ct)
    {
        if (ThrowOnSave is not null)
        {
            throw ThrowOnSave;
        }

        await inner.SaveChangesAsync(ct);
        SaveCount++;
        calls.Add("save");
    }
}

/// <summary>
/// Entorno de pruebas de casos de uso: repositorios y unidad de trabajo REALES sobre EF InMemory, con el seed inicial
/// (usuario@test.com, admin@test.com y límites 1.000–100.000), reloj controlable y usuario actual simulado.
/// </summary>
public sealed class TestApp : IDisposable
{
    private TestApp(CblDbContext db)
    {
        Db = db;
        Cache = new RecordingReadCache(Calls);
        Uow = new CountingUnitOfWork(new UnitOfWork(db), Calls);
        Users = new UserRepository(db);
        Loans = new LoanRepository(db);
        Parameters = new SystemParameterRepository(db);
        Audits = new LoanAuditRepository(db);
        Correlation.CorrelationId.Returns("corr-123");
    }

    public CblDbContext Db { get; }

    public FakeClock Clock { get; } = new();

    public List<string> Calls { get; } = [];

    public RecordingReadCache Cache { get; }

    public CountingUnitOfWork Uow { get; }

    public ICurrentUser CurrentUser { get; } = Substitute.For<ICurrentUser>();

    public ICorrelationIdAccessor Correlation { get; } = Substitute.For<ICorrelationIdAccessor>();

    public IUserRepository Users { get; }

    public ILoanRepository Loans { get; }

    public ISystemParameterRepository Parameters { get; }

    public ILoanAuditRepository Audits { get; }

    public static async Task<TestApp> CreateAsync()
    {
        var app = new TestApp(TestDb.Create());
        await InMemorySeeder.SeedAsync(app.Db, app.Clock);

        return app;
    }

    public void SignInAs(Guid id, Role role)
    {
        CurrentUser.IsAuthenticated.Returns(true);
        CurrentUser.UserId.Returns(id);
        CurrentUser.Role.Returns(role);
    }

    public void SignInAs(User user) => SignInAs(user.Id, user.Role);

    public async Task<User> AddUserAsync(Role role = Role.Borrower, string? email = null)
    {
        var user = Builders.NewUser(role, email);
        Db.Users.Add(user);
        await Db.SaveChangesAsync();

        return user;
    }

    public async Task<Loan> AddLoanAsync(
        Guid? borrowerId = null,
        decimal amount = 5000m,
        int termMonths = 12,
        DateTime? createdAt = null)
    {
        var loan = Loan.Request(borrowerId ?? SeedData.BorrowerId, amount, termMonths, createdAt ?? Clock.UtcNow);
        Db.Loans.Add(loan);
        await Db.SaveChangesAsync();

        return loan;
    }

    public LoanLimitsReader Limits => new(Parameters, Cache);

    public RequestLoanHandler RequestLoan => new(Loans, Users, Uow, CurrentUser, Clock, Limits);

    public UpdateLoanHandler UpdateLoan => new(Loans, Uow, CurrentUser, Cache, Limits);

    public DeleteLoanHandler DeleteLoan => new(Loans, Uow, CurrentUser, Cache);

    public DecideLoanHandler DecideLoan => new(Loans, Users, Audits, Uow, CurrentUser, Clock, Cache, Correlation);

    public ApproveLoanHandler ApproveLoan => new(DecideLoan);

    public RejectLoanHandler RejectLoan => new(DecideLoan);

    public GetMyLoansHandler GetMyLoans => new(Loans, CurrentUser);

    public GetAllLoansHandler GetAllLoans => new(Loans);

    public GetLoanByIdHandler GetLoanById => new(Loans, CurrentUser);

    public GetLoanStatusHandler GetLoanStatus => new(Loans, CurrentUser, Cache);

    public CreateUserHandler CreateUser => new(Users, Hasher, Uow, Clock);

    public UpdateUserHandler UpdateUser => new(Users, Hasher, Uow, CurrentUser);

    public GetUsersHandler GetUsers => new(Users);

    public GetUserByIdHandler GetUserById => new(Users);

    public GetLoanLimitsHandler GetLoanLimits => new(Limits);

    public UpdateLoanLimitsHandler UpdateLoanLimits => new(Parameters, Uow, CurrentUser, Clock, Cache);

    public IPasswordHasher Hasher { get; } = new PasswordHasherAdapter();

    public void Dispose() => Db.Dispose();
}
