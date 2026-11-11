# Tasks: cbl-backend (.NET 8) / Tareas del backend

Conventions: `[P]` = can run in parallel with sibling tasks that share the same dependencies. `Dep` = must be done first. Each task ends with an acceptance check (AC). Code in English. Projects: `Cbl.Domain`, `Cbl.Application`, `Cbl.Infrastructure`, `Cbl.Api`, `Cbl.UnitTests`, `Cbl.IntegrationTests`, `db/Cbl.Database`.

Note: ADR-0009 (15-30 min JWT) and ADR-0017 (role `Usuario`) are outdated vs CONTEXT.md; this plan follows CONTEXT.md (10 min, `Admin`/`Borrower`). Update those ADRs in task B-00.

**MVP cut line**: everything in Phases 0-7 and the MVP-marked tasks of Phase 8-9 (login -> request -> approve/reject -> list -> cache -> concurrency -> unit tests). Tasks marked `(post-MVP)` are cut first if time runs out.

---

## Phase 0 - Housekeeping / Preparación
- **B-00** Rename "Loans" to "Cbl" in docs, fix ADR-0009/0017 per note above. Dep: none. AC: `grep -ri "loans-" docs` returns nothing relevant; ADRs say 10 min and `Borrower`.

## Phase 1 - Scaffold / Estructura de la solución
- **B-01** Create `Cbl.sln`, `global.json` (.NET 8), `Directory.Build.props` (nullable on, warnings as errors, implicit usings), `.editorconfig`, `.gitignore`. Dep: B-00. AC: `dotnet build` succeeds on empty solution.
- **B-02** Create projects with references: Domain (no refs) <- Application <- Infrastructure; Api -> Application + Infrastructure; UnitTests -> Domain/Application; IntegrationTests -> Api. Dep: B-01. AC: build OK; Domain has zero package references; no project references upward.
- **B-03** [P] Add packages: Application (FluentValidation), Infrastructure (EF Core SqlServer + InMemory, Microsoft.Extensions.Identity.Core for `PasswordHasher`), Api (JwtBearer, Swashbuckle or Microsoft.AspNetCore.OpenApi), tests (xUnit, NSubstitute, FluentAssertions, Mvc.Testing). Dep: B-02. AC: restore OK, versions pinned.
- **B-04** [P] Add `db/Cbl.Database` (Microsoft.Build.Sql SDK) empty project. Dep: B-01. AC: `dotnet build db/Cbl.Database` produces `.dacpac`.

## Phase 2 - Domain model / Modelo de dominio (Dep: B-02)
- **B-10** Enums: `Role { Admin, Borrower }`, `LoanStatus { Pending, Approved, Rejected }`. AC: compiles.
- **B-11** `User` entity: Id (Guid), Email (unique, normalized), PasswordHash, Role, IsActive, FailedLoginCount, LockoutUntil, CreatedAt; methods `RegisterFailedLogin(max, window, now)`, `ResetLoginFailures()`, `IsLockedOut(now)`, `Deactivate()`. AC: unit-testable without infrastructure.
- **B-12** `Loan` entity: Id, BorrowerId, Amount (decimal 18,2), TermMonths, Status, RequestedAt, DecidedByUserId?, DecidedAt?, Version (int, own concurrency token). Factory `Loan.Request(...)` validates term 1-60 and amount > 0; methods `Approve(adminId, now)`, `Reject(adminId, now)` throw `InvalidLoanTransitionException` unless Pending; `UpdateTerms(...)` and `EnsureCanDelete()` only while Pending. AC: decided loan can never change.
- **B-13** [P] `LoanDecisionAudit` entity: Id, LoanId, AdminUserId, Decision (Approved/Rejected), PreviousStatus, DecidedAt, CorrelationId?. Created by the Loan decision (or by the use case in the same unit of work). AC: append-only (no update methods).
- **B-14** [P] `SystemParameter` entity: Key (string PK), Value (string), UpdatedAt, UpdatedBy; constants `LoanAmountMin`, `LoanAmountMax`. AC: compiles.
- **B-15** Domain exceptions: `DomainException` base, `InvalidLoanTransitionException`, `LoanAmountOutOfRangeException`. Dep: B-12. AC: used by entity tests later.

## Phase 3 - SQL project / Proyecto SQL (Dep: B-04, B-10..B-14)
- **B-20** [P] `Tables/Users.sql`: PK, `UNIQUE(Email)`, `CHECK(Role IN ('Admin','Borrower'))`, lockout columns. AC: dacpac builds.
- **B-21** [P] `Tables/Loans.sql`: PK, FK to Users (borrower and decider), `CHECK(TermMonths BETWEEN 1 AND 60)`, `CHECK(Amount > 0)`, `CHECK(Status IN (...))`, `Version INT NOT NULL DEFAULT 1`, index `(BorrowerId, Status)`, index `(Status, RequestedAt)`. AC: builds.
- **B-22** [P] `Tables/LoanDecisionAudit.sql`: FK to Loans/Users, index on `LoanId`. AC: builds.
- **B-23** [P] `Tables/SystemParameters.sql`: `[Key] NVARCHAR(100) PK`, `Value NVARCHAR(500)`. AC: builds.
- **B-24** `PostDeployment/Seed.sql` (idempotent `MERGE`): `usuario@test.com` (Borrower) and `admin@test.com` (Admin), password `123` as precomputed PBKDF2 hash (generate once with a throwaway console snippet using the same `PasswordHasher` version; record in docs that it is sample-only); `LoanAmountMin=1000`, `LoanAmountMax=100000`. Dep: B-20, B-23. AC: deploy twice to a SQL Server leaves 2 users and 2 parameters; hash verifies in a unit test (B-95).
- **B-25** Wire `Script.PostDeployment.cs`-style include (`:r .\Seed.sql`). Dep: B-24. AC: dacpac contains post-deploy script.

## Phase 4 - EF mapping / Mapeo EF (Dep: B-10..B-14)
- **B-30** `CblDbContext` with `DbSet`s; `OnModelCreating` maps tables/columns/lengths/precision to match the `.sqlproj` exactly (table names `Users`, `Loans`, `LoanDecisionAudit`, `SystemParameters`; enums as strings; decimal(18,2)). AC: model builds under InMemory.
- **B-31** Concurrency: `Loan.Version` configured with `IsConcurrencyToken()`; incremented in the entity on every state change (not `rowversion`, so InMemory also checks it). Dep: B-30. AC: unit test with two contexts on the same InMemory store raises `DbUpdateConcurrencyException`.
- **B-32** [P] Configure FKs, delete behavior `Restrict`, unique index on `Users.Email`. AC: matches SQL.
- **B-33** Provider switch in `AddInfrastructure(config)`: `Database:Provider = InMemory | SqlServer` (default InMemory in Development). `InMemory` seeds the same two users and parameters at startup (code seeder, dev only). AC: API starts locally with no DB.
- **B-34** Repositories + Unit of Work (ports in Application, adapters in Infrastructure): `IUserRepository`, `ILoanRepository`, `ISystemParameterRepository`, `ILoanAuditRepository`, `IUnitOfWork.SaveChangesAsync`. AC: no EF types leak into Application.

## Phase 5 - Application use cases / Casos de uso (Dep: B-15, B-34)
Plain classes in `Cbl.Application`, one per use case, DTOs as records, FluentValidation validators; no MediatR.
- **B-40** Ports/abstractions: `IClock`, `IPasswordHasher`, `ITokenService`, `ICurrentUser` (Id, Role from token), `IReadCache`. AC: compiles.
- **B-41** Command `Login` (email, password): generic failure message, lockout via `User` methods, returns token + expiry + role. AC: unknown email and bad password return identical error.
- **B-42** Command `RequestLoan` (amount, term): reads limits from `SystemParameters` (via cached reader), borrower id from `ICurrentUser`, creates Pending loan. AC: out-of-range amount/term rejected with field errors.
- **B-43** Command `ApproveLoan` and `RejectLoan` (admin id from token): load loan, call entity method, add audit row, single `SaveChangesAsync`; on success invalidate cache keys (after save). AC: second decision -> domain exception; concurrency exception surfaces as `ConcurrencyConflictException`.
- **B-44** Queries `GetMyLoans`, `GetLoanById` (owner or Admin; else NotFound to avoid enumeration), `GetLoanStatus` (cached), `GetAllLoans` (Admin, filter by status, paging `page/pageSize` max 50). AC: borrower never receives another borrower's loan.
- **B-45** [P] Commands `UpdateLoan` (owner, Pending only) and `DeleteLoan` (owner or Admin, Pending only). (post-MVP if tight; PDF asks for CRUD, so keep if time allows.) AC: decided loan -> conflict error.
- **B-46** [P] User use cases (Admin): `CreateUser`, `GetUsers`, `GetUserById`, `UpdateUser` (name/role/active/optional password reset), `DeactivateUser` (logical; cannot deactivate self; last active admin protected). AC: email unique; password hashed. (post-MVP for password reset only.)
- **B-47** [P] Parameters: `GetLoanLimits` (any authenticated), `UpdateLoanLimits` (Admin; min < max, min > 0), invalidates the parameter cache entry. AC: next `RequestLoan` uses new limits.
- **B-48** Application exceptions: `NotFoundException`, `ForbiddenException`, `ValidationFailedException`, `ConflictException`, `ConcurrencyConflictException`. AC: mapped in B-71.
- **B-49** DI extension `AddApplication()`. AC: all use cases resolvable.

## Phase 6 - Auth, JWT, policies / Autenticación y autorización (Dep: B-40)
- **B-50** `JwtOptions` (Issuer, Audience, Key, `LifetimeMinutes=10`) bound from config; key from env var / user-secrets, dev fallback only in Development; fail fast if missing elsewhere. AC: app refuses to start in Production without a key >= 32 bytes.
- **B-51** `JwtTokenService`: claims `sub`, `email`, `role`, `jti`; HS256. AC: unit test decodes claims and exp = now + 10 min.
- **B-52** `AddAuthentication().AddJwtBearer`: validate issuer, audience, signature, lifetime, `ClockSkew = 30s`; no refresh token. AC: expired token -> 401.
- **B-53** Policies: `AdminOnly` (role Admin), `BorrowerOnly` (role Borrower), fallback policy = authenticated user; `[AllowAnonymous]` only on login/health. AC: new endpoint is protected by default.
- **B-54** `CurrentUser` implementation from `HttpContext` claims (anti-IDOR: never accept user id from body/route for ownership). AC: no request DTO contains a user id for loan creation.
- **B-55** Lockout config `Auth:MaxFailedAttempts=5`, `Auth:LockoutMinutes=15` (options, validated). AC: 5 failures lock, success after window unlocks.

## Phase 7 - Security middleware pipeline / Pipeline de seguridad (Dep: B-52)
Order in `Program.cs` must be exactly: exception handler -> HSTS/HTTPS redirection -> security headers -> CORS -> rate limiter -> correlation id/logging -> authentication -> authorization -> endpoints.
- **B-60** [P] Security headers middleware: `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, `Content-Security-Policy: default-src 'none'; frame-ancestors 'none'` (relax only for Swagger in Development), `Cache-Control: no-store` on API responses. AC: headers present on any response, including errors.
- **B-61** [P] CORS: named policy with origin from `Cors:AllowedOrigins` (frontend), methods/headers explicit, no `AllowAnyOrigin`, no credentials. AC: disallowed origin gets no CORS headers.
- **B-62** [P] Rate limiting (`AddRateLimiter`): global fixed window per IP (e.g. 100/min) and policy `login` (e.g. 5/min per IP), 429 with `ProblemDetails` and `Retry-After`; configurable. AC: 6th login in a minute -> 429.
- **B-63** [P] Correlation ID middleware (`X-Correlation-Id` in/out, added to log scope and ProblemDetails `traceId`); request logging that never logs bodies, passwords or tokens. AC: log output has no `password`/`Authorization` values.
- **B-64** [P] Request body size limit (e.g. 64 KB) via Kestrel `MaxRequestBodySize`; HTTPS redirection + HSTS outside Development. AC: oversized body -> 413.

## Phase 8 - API endpoints / Endpoints (Dep: B-49, B-53, Phase 7 for final wiring)
Controllers or minimal-API groups, thin: map request -> use case -> DTO. Route table (all under `/api`, JWT required unless stated):

| Method | Route | Policy | Purpose | Success | Errors |
|---|---|---|---|---|---|
| POST | `/api/auth/login` | anonymous, rate-limit `login` | Login | 200 token | 400, 401, 423 (locked), 429 |
| GET | `/api/auth/me` | authenticated | Current user and role | 200 | 401 |
| POST | `/api/loans` | BorrowerOnly | Request loan (amount, termMonths) | 201 + Location | 400/422 |
| GET | `/api/loans/mine` | BorrowerOnly | My loans | 200 | 401, 403 |
| GET | `/api/loans/{id}` | owner or Admin | Loan detail | 200 | 404 |
| GET | `/api/loans/{id}/status` | owner or Admin | Status only (cached) | 200 | 404 |
| GET | `/api/loans?status=&page=&pageSize=` | AdminOnly | All loans | 200 | 403 |
| PUT | `/api/loans/{id}` | BorrowerOnly (owner) | Edit while Pending | 200 | 404, 409 |
| DELETE | `/api/loans/{id}` | owner or Admin | Remove while Pending | 204 | 404, 409 |
| POST | `/api/loans/{id}/approve` | AdminOnly | Approve | 200 | 404, 409 |
| POST | `/api/loans/{id}/reject` | AdminOnly | Reject | 200 | 404, 409 |
| GET | `/api/loans/{id}/audit` | AdminOnly | Decision audit trail | 200 | 404 |
| GET | `/api/users` | AdminOnly | List users | 200 | 403 |
| POST | `/api/users` | AdminOnly | Create user | 201 | 400/422, 409 (email) |
| GET | `/api/users/{id}` | AdminOnly | User detail | 200 | 404 |
| PUT | `/api/users/{id}` | AdminOnly | Update user | 200 | 404, 409 |
| DELETE | `/api/users/{id}` | AdminOnly | Deactivate (logical) | 204 | 404, 409 |
| GET | `/api/parameters/loan-limits` | authenticated | Current min/max (for form validation) | 200 | 401 |
| PUT | `/api/parameters/loan-limits` | AdminOnly | Change limits | 200 | 400/422 |
| GET | `/health` | anonymous | Liveness | 200 | - |

- **B-70** `Program.cs` composition root + endpoint registration helper, route table above as the checklist. Dep: B-49. AC: all rows implemented (MVP rows: login, me, POST loans, loans/mine, loans/{id}/status, GET loans, approve, reject, loan-limits GET).
- **B-71** Global error handling: `IExceptionHandler` + `AddProblemDetails`; mapping: ValidationFailed -> 422 with `errors` per field (or 400 for malformed JSON), Unauthorized -> 401, Forbidden -> 403, NotFound -> 404, Conflict/InvalidLoanTransition/ConcurrencyConflict -> 409, rate limit -> 429, unknown -> 500 with generic text (no stack trace/exception message outside Development). AC: every error response has `application/problem+json`, `type`, `title`, `status`, `traceId`.
- **B-72** FluentValidation auto-run in endpoint filter/action filter; request DTOs separate from response DTOs (never return entities; never expose `PasswordHash`, lockout fields). AC: invalid body never reaches use case.
- **B-73** [P] Remaining non-MVP endpoints (PUT/DELETE loans, audit, users CRUD, PUT limits). Dep: B-45..B-47. AC: rows above done.

## Phase 9 - Cache and concurrency / Caché y concurrencia (Dep: B-43, B-44)
- **B-80** `IReadCache` adapter over `IMemoryCache` (size-limited, absolute TTL 5 min, keys `loan-status:{id}`, `loan-limits`). AC: unit test on adapter get/set/remove.
- **B-81** `GetLoanStatus` read-through caching; owner/admin check happens before returning cached data (cache holds status+owner id, authorization is not cached). AC: borrower B cannot read A's cached status.
- **B-82** Invalidation: Approve/Reject remove `loan-status:{id}` only after successful `SaveChanges`; `UpdateLoanLimits` removes `loan-limits`. AC: unit test with fake cache asserts removal order (after save) and no removal when save throws.
- **B-83** Concurrency end-to-end: `DbUpdateConcurrencyException` caught in the unit of work and rethrown as `ConcurrencyConflictException` -> 409; audit and loan saved in one `SaveChanges` (one implicit transaction). AC: covered by B-96 (unit) and B-102 (CI).

## Phase 10 - Unit tests / Pruebas unitarias (local, InMemory/mocks; Dep: related implementation)
Run with `dotnet test --filter "Category!=Integration"`.
- **B-90** Domain `Loan`: valid request; term 0/61 rejected; Approve/Reject from Pending; second decision throws; version increments. 
- **B-91** Domain `User`: lockout after N failures, reset on success, expiry of lockout.
- **B-92** `RequestLoan`: within limits OK; below min / above max rejected; limits taken from parameters; borrower id from `ICurrentUser`.
- **B-93** `ApproveLoan`/`RejectLoan`: audit row written with admin id and timestamp; one save; second decision conflict; cache invalidated after save only; concurrency exception mapped.
- **B-94** `GetMyLoans`/`GetLoanById`/`GetLoanStatus`: ownership enforced; cache hit avoids repository call; cache refreshed after decision.
- **B-95** `Login` and `JwtTokenService`: generic failure message, lockout, claims, 10 min expiry, seed hash for `123` verifies.
- **B-96** EF InMemory concurrency test: two contexts, same loan, second `SaveChanges` throws (proves `Version` token works).
- **B-97** [P] Validators (amount, term, email, password length) and ProblemDetails mapping table (exception -> status). 
AC for phase: all green locally with no Docker; critical operations (request, approve, reject, cache, auth) covered.

## Phase 11 - Integration tests, CI-only / Pruebas de integración (solo CI)
Infra: `WebApplicationFactory<Program>` pointing at SQL Server from the GitHub Actions service container, schema deployed with `SqlPackage` from the `.dacpac` (not Testcontainers, since there is no local Docker; CI service container is enough). Every test class is tagged `[Trait("Category","Integration")]` and guarded by env var `CBL_TEST_SQL`: if unset the tests are skipped (custom `[SqlServerFact]` attribute sets `Skip`). Local default: `dotnet test` shows them as skipped, not failed.
- **B-100** Test host fixture + `[SqlServerFact]` + DB reset between tests (delete rows then re-run seed) + token helper (logs in with seed users). AC: locally all integration tests Skipped; in CI executed.
- **B-101** Scenarios 1-4 and 9: no token -> 401; Borrower approving -> 403; valid request -> 201; invalid -> 422 with field errors; borrower cannot see another's loan; approve changes status and writes audit; errors are `ProblemDetails`.
- **B-102** Scenarios 5-6: decide twice -> 409; race: two concurrent approve/reject calls (`Task.WhenAll`), exactly one 200 and one 409, exactly one audit row.
- **B-103** Scenarios 7-8: cache returns fresh status after decision; changing loan limits affects new requests.
- **B-104** Scenario 10: login rate limiting -> 429; lockout after 5 bad passwords.
- **B-105** Scenario 11: schema drift check - compare `Model` (EF metadata: tables, columns, types, nullability, max length, FKs, unique index) with `INFORMATION_SCHEMA`/`sys.*` of the deployed dacpac; fail on any difference. Also assert the seed (2 users, 2 parameters) exists after deploy. AC: intentionally changing a column length in OnModelCreating makes it fail.
- **B-106** CI hook (coordinates with CI owner): workflow builds `.sqlproj` -> dacpac, starts `mcr.microsoft.com/mssql/server` service container, runs `sqlpackage /Action:Publish`, sets `CBL_TEST_SQL`, runs `dotnet test` incl. integration. AC: green pipeline; CD step uploads dacpac + `dotnet publish` zip as artifacts only.

## Phase 12 - OpenAPI / Documentación de la API (Dep: B-70)
- **B-110** Swagger/OpenAPI generation, enabled **only in Development** (`app.Environment.IsDevelopment()`), with JWT bearer security scheme, grouped tags, XML comments, `ProducesResponseType` for 200/201/400/401/403/404/409/422/429 using `ProblemDetails`. AC: Swagger UI "Authorize" works with the login token; Production returns 404 for `/swagger`.
- **B-111** Export static `openapi.json` at build/CI (`dotnet-swagger` or build-time document generation) and attach it as CI artifact and to docs (feeds the frontend contract). AC: file present in CI artifacts. (post-MVP)

## Phase 13 - Wrap-up / Cierre
- **B-120** README (EN/ES): run locally with InMemory (`dotnet run`), seed credentials (sample-only), how to run tests, how integration tests are skipped locally, env vars (`Jwt__Key`, `Cors__AllowedOrigins`, `Database__Provider`). Dep: all MVP. AC: a fresh clone runs in < 5 min.
- **B-121** Update `docs/speckit-log.md` and ADRs for any deviation. AC: log entry per phase.

---

## Suggested order and parallelism
B-00 -> B-01 -> B-02 -> (B-03 [P] B-04) -> Domain (B-10..B-15) -> (SQL B-20..B-25 [P] with EF B-30..B-34) -> Application (B-40..B-49) -> (Auth B-50..B-55 [P] with Middleware B-60..B-64) -> API B-70..B-73 -> Cache/concurrency B-80..B-83 -> Unit tests (written alongside each phase, closed in Phase 10) -> Integration B-100..B-106 -> OpenAPI B-110 -> B-120.
MVP stops after B-70 (MVP rows), B-71, B-72, Phase 9, Phase 10 and B-120; integration tests B-100..B-103 and B-106 are the minimum for CI value.

## Open questions for the user
1. **Loan edit/delete (PUT/DELETE `/api/loans/{id}`)**: the PDF says "CRUD of loans" but decisions are final. Recommended default: Borrower may edit/delete only their own loan while Pending; Admin may delete Pending only; decided loans are immutable.
2. **Can an Admin also request loans?** Recommended default: no, `POST /api/loans` is `BorrowerOnly`; admins only decide and manage.
3. **Password policy for admin-created users** (seed uses `123`): Recommended default: min 8 chars on create/reset via API; the seed `123` is sample-only and documented as such.
4. **Cache TTL for loan status**: Recommended default: 5 minutes absolute TTL plus explicit invalidation on decision.
5. **Integration tests infrastructure**: Recommended default: GitHub Actions SQL Server service container + `SqlPackage` (no Testcontainers), skipped locally via `CBL_TEST_SQL` env var.
