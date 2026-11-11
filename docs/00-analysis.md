🇪🇸 [Español](00-analisis.es.md)

# Analysis of the Makers technical test (adapted to .NET + Angular) – CBL Project (Control Banking Loans)

## 1. Scope
- **Theory (30%)**: 6 open questions, answered in writing and justified.
- **Practice (70%)**: loans REST API + Angular frontend. Code in a public GitHub repository. Estimate: 2 h practice, 60 min theory.
- Adaptation: the PDF is written for Java/Spring Boot; here it is solved in **.NET** (backend) and **Angular** (frontend), in **two repositories** (`cbl-backend`, `cbl-frontend`).

## 2. Stack translation
| PDF requirement | .NET equivalent |
|---|---|
| Spring Boot REST | ASP.NET Core Web API (.NET 8 LTS) |
| Spring Data JPA / Hibernate | Entity Framework Core |
| Hibernate Validator | FluentValidation |
| Spring Security | JWT Bearer + policies and roles |
| Spring WebFlux (where it fits) | end-to-end `async/await` |
| Clean/hexagonal architecture | Domain, Application, Infrastructure, Api |
| EHCache | `IMemoryCache` behind an interface |
| `@Transactional` | Unit of Work with atomic `SaveChanges` |
| JUnit 5 + Spring Boot Test | xUnit + Moq/NSubstitute + FluentAssertions + `WebApplicationFactory` |
| H2 / PostgreSQL / MySQL | EF InMemory (local/unit) and real SQL Server (CI only) |
| Clear HTTP errors | `ProblemDetails` (RFC 7807) with `IExceptionHandler` |

## 3. Functional requirements
- RF-01 Request a loan (amount and term in months).
- RF-02 Admin approves or rejects.
- RF-03 The Borrower queries the status of their loans.
- RF-04 CRUD of loans and users respecting business rules.
- RF-05 Login with JWT, roles `Borrower` and `Admin`.
- RF-06 Frontend: submit requests, query status; admin sees and changes statuses.

Sample credentials from the PDF (seed data): `usuario@test.com` / `123` and `admin@test.com` / `123`.

## 4. Non-functional requirements
RNF-01 Clean architecture · RNF-02 Status cache with invalidation · RNF-03 Role-based authorization · RNF-04 Transactional approval and concurrency · RNF-05 Unit tests · RNF-06 Clear HTTP errors · RNF-07 Reactive Forms · RNF-08 Guards · RNF-09 Safe state (NgRx) · RNF-10 UI close to the mockup.

## 5. Ambiguities and decisions taken
| # | Topic | Decision |
|---|---|---|
| 1 | Term | Included, in months (the mockup shows only the amount) |
| 2 | Business rules | Term 1–60 months; decisions only on `Pending`; final, immutable decision; **amount limits in a database parameter** (default 1,000–100,000) |
| 3 | Statuses | `Pending`, `Approved`, `Rejected` |
| 4 | User CRUD | Managed by the admin; seed data; no self-registration |
| 5 | What "Approve" does | Changes status, records who and when, leaves an audit entry, all atomic |
| 6 | Interest and installments | Out of scope, extensible design |
| 7 | Theory question 6 (Spring) | Answered with the ASP.NET Core equivalent and a brief mention of Spring |
| 8 | State in Angular | NgRx |
| 9 | Database | `.sqlproj` as the source of truth, no EF migrations; EF InMemory for local/unit; real SQL Server in CI only |
| 10 | Repositories | Two: backend and frontend |
| 11 | CQRS | Light, by convention, no MediatR |
| 12 | Token in frontend | In memory only, short-lived (10 minutes), no refresh token |

## 6. Race condition
The only real risk point: two admins decide on the same loan. It is solved with a transition validated in the domain, a version column (optimistic concurrency) and a 409 response. Cache invalidation happens after the save is confirmed. The race test runs against a real SQL Server, in CI only.

## 7. Security (middleware pipeline)
1. Global exception handling → `ProblemDetails`
2. HTTPS redirection + HSTS
3. Security headers
4. CORS restricted to the frontend origin
5. Rate limiting (stricter on `/login`)
6. Correlation ID + logging without sensitive data
7. Authentication (JWT Bearer)
8. Authorization (policies, `AdminOnly`)

Additional: the user id always comes from the token (anti-IDOR), input/output DTOs, parameterized queries, generic login message and temporary lockout, secrets outside the repo, Swagger only in Development, request body size limit.

## 8. Integration tests (minimum)
They run only in CI (SQL Server service container); locally they are skipped unless `CBL_TEST_SQL` is set (ADR-0021).
1. No token → 401; non-admin approving → 403.
2. Valid request → 201; invalid → 400/422 with per-field errors.
3. A borrower does not see another borrower's loans.
4. Approve/reject changes status and leaves an audit entry.
5. Deciding twice → 409.
6. Race: two simultaneous decisions, only one prevails.
7. Cache updated after deciding.
8. Changing the limits parameter affects new requests.
9. Errors in `ProblemDetails` format.
10. Rate limiting on login.
11. The EF mapping matches the `.sqlproj` schema.

## 9. Proposed structure
```
/src   CBL.Domain · CBL.Application · CBL.Infrastructure (DbContext + OnModelCreating) · CBL.Api
/tests CBL.UnitTests · CBL.IntegrationTests
/db    CBL.Database (Microsoft.Build.Sql): Tables/ (Users, Loans, LoanDecisionAudit, SystemParameters), PostDeployment/Seed.sql
/.github/workflows
```
CI flow (GitHub Actions): build `.sqlproj` → `.dacpac`; start SQL Server as a service container and deploy with `SqlPackage`; run unit and integration tests; on the main branch, publish the `.dacpac` (after a `DeployReport`). CD: pushing a `v*.*.*` tag publishes the artifacts in a GitHub Release (ADR-0024).

## 10. Risks
- With EF InMemory, atomicity is not guaranteed by the engine: it is compensated with tests on a real SQL Server (in CI).
- The scope is large for 2 h: prioritize the MVP (login and roles → request → approve/reject → list → cache and tests).
