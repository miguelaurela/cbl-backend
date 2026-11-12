🇪🇸 [Español](README.es.md)

# CBL Project (Control Banking Loans) – Backend

REST API in ASP.NET Core for the Makers technical test (.NET + Angular version).
The frontend lives in a separate repository: [`cbl-frontend`](https://github.com/miguelaurela/cbl-frontend).

> **For reviewers:** the **theory part** is in this README, in the [Theory part](#theory-part) section. The code is in `src/`, the tests in `tests/`, and how to run it in [How to run](#how-to-run-local-no-docker-no-sql-server).

## Theory part

### 1. Optimization in financial systems
- **Concurrency**: asynchronous I/O, no thread blocking. Optimistic concurrency (version column) on low-contention entities; pessimistic locking only where contention is high and the retry cost is unacceptable.
- **Cache**: on the read side (light CQRS), with explicit invalidation after the save is confirmed. Evolution: distributed cache (Redis) when scaling horizontally.
- **Scalability**: stateless services, reads that scale with replicas or read models, queues for non-critical work, idempotency in write operations.
- **Patterns**: Unit of Work, Repository, CQRS, Outbox.

### 2. Security in financial APIs
- **SQL Injection**: parameterized queries (EF Core), never concatenate SQL.
- **CSRF**: does not apply with `Authorization: Bearer` in a header; it would apply if the token lived in cookies (SameSite, anti-CSRF tokens).
- **XSS**: encoded output, CSP and security headers; keeping the token in memory reduces the impact.
- **Others**: short-lived JWT with full validation, role-based and resource-based authorization (anti-IDOR), rate limiting, DTOs (anti mass assignment), HTTPS/HSTS, restricted CORS, secrets outside the repo, errors without internal details, logs without sensitive data.

### 3. Transactions in distributed systems
There is no single ACID transaction across services. We use:
- **Saga** (orchestrated or choreographed) with compensations.
- **Outbox** to publish events atomically with the local change.
- **Idempotency** with idempotency keys and retries with backoff.
- **Errors**: explicit intermediate states (`Pending`, `Failed`, `Compensated`), timeouts, dead-letter queues and traceability with a correlation ID.
- Inside a single service, a local transaction and optimistic concurrency (409 on conflict).

### 4. Unit and integration tests
- **Unit** (xUnit, Moq/NSubstitute, FluentAssertions): domain, validators and use cases.
- **Integration** (`WebApplicationFactory` + SQL Server as a service container in CI, with the `.dacpac` schema): authentication, authorization, isolation between users, decisions, concurrency, cache, error format and consistency between the EF mapping and the schema. Locally they are skipped unless `CBL_TEST_SQL` is set.
- **Front-end** (Karma + Jasmine): reducers, selectors, effects, guards, interceptors, forms and components, plus contract checks (types generated from the OpenAPI) and security checks (no `localStorage` and no unsanitized HTML).
- **CI**: everything runs on every change.

### 5. Front-end
- **State**: NgRx (actions, reducers, effects, selectors) with the `auth`, `loans` (including the amount limits) and `users` slices. Only effects call the API; components dispatch actions and read selectors.
- **Authentication**: short-lived JWT (10 minutes) kept only in memory (never in `localStorage`, `sessionStorage` or cookies); the interceptor adds the Bearer and logs out on 401, and a timer logs out on expiry. "Cerrar sesión" calls `POST /api/auth/logout` to revoke the token. Trade-off: it is lost on refresh. Evolution: refresh token in an `HttpOnly` cookie.
- **Routes**: `authGuard`, `adminGuard`, `borrowerGuard` and `guestGuard`; after login the user returns to the requested route, validated against open redirects. The backend always validates again.
- **Forms and errors**: Reactive Forms with client-side validation; server validation errors (422) are mapped to each field, and 409, 423 (lockout with `Retry-After`) and 429 show specific messages.
- **Consistency**: after deciding a loan, effects update the state from the API response; a double decision (409) is reported and the data reloaded. Repeated decisions on the same loan are serialized in the effect.

### 6. Spring Boot (ASP.NET Core equivalent)
- **a) `@SpringBootApplication`**: combines `@Configuration`, `@EnableAutoConfiguration` and `@ComponentScan`; it starts the context, registers beans and applies conditional auto-configuration. In ASP.NET Core: `WebApplication.CreateBuilder` sets up host, configuration, logging and DI; `builder.Build()` and `app.Run()` assemble the pipeline and start.
- **b) Bean lifecycle**: instantiation → injection → `BeanPostProcessor`/`@PostConstruct` → use → `@PreDestroy`. You intervene with those hooks, `@Scope` and `BeanPostProcessor`. In .NET: `Singleton`, `Scoped`, `Transient` lifetimes, `IHostedService` and `IDisposable`.
- **c) Auto-configuration without breaking convention over configuration**: properties (`application.yml`), `@ConfigurationProperties`, `@ConditionalOnMissingBean` to replace only what is needed and a targeted `exclude`. In .NET: the Options Pattern and `Configure<T>`.

## Stack
.NET 8 · ASP.NET Core Web API · EF Core · JWT · FluentValidation · xUnit · SQL Server (`.sqlproj` project)

## Structure
```
/src      CBL.Domain · CBL.Application · CBL.Infrastructure · CBL.Api
/tests    CBL.UnitTests · CBL.IntegrationTests
/db       CBL.Database (SQL project, source of truth for the schema)
/docs     analysis, Spec Kit log, theory, planning and ADRs
/specs    Spec Kit artifacts (specs/001-gestion-prestamos)
/.specify Spec Kit constitution, templates and scripts
```

## Current state
The implementation (Spec Kit phase 7) is **complete** on `main`: all eight user stories (login, request a loan, my loans and status, admin list, approve/reject with audit and concurrency control, user management, loan limits and logout), the CI and release workflows and the polish tasks are done. Tasks are tracked in [`specs/001-gestion-prestamos/tasks.md`](specs/001-gestion-prestamos/tasks.md) and the live validation of the quickstart is in [`quickstart-results.md`](specs/001-gestion-prestamos/quickstart-results.md). Next: the frontend (`cbl-frontend`), which must call `POST /api/auth/logout` and reconcile with this OpenAPI contract.

## How to run (local, no Docker, no SQL Server)
Requirements: .NET SDK 8 (`global.json` pins it).

```powershell
dotnet restore
dotnet build
dotnet test                       # unit tests; integration tests are skipped (see below)

$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:Jwt__Key = "<a development key of at least 32 characters>"
dotnet run --project src/CBL.Api
```

In Development the API uses **EF InMemory** seeded with the sample data and serves Swagger UI at `/swagger` (Swagger is never enabled outside Development). The JWT key is **mandatory** and must never be committed: use an environment variable or `dotnet user-secrets`.

Sample credentials (**sample only**, documented as such): `usuario@test.com` (Borrower) and `admin@test.com` (Admin), password `123`.

### Environment variables
| Variable | Purpose | Default |
|---|---|---|
| `Jwt__Key` | JWT signing key (≥ 32 bytes). **Required.** | – |
| `Jwt__Issuer`, `Jwt__Audience` | Token issuer and audience | `cbl-api`, `cbl-clients` |
| `Database__Provider` | `InMemory` or `SqlServer` | `InMemory` in Development, `SqlServer` otherwise |
| `ConnectionStrings__Cbl` | SQL Server connection string (when the provider is `SqlServer`) | – |
| `Cors__AllowedOrigins__0` | Frontend origin allowed by CORS | `http://localhost:4200` |
| `Auth__MaxFailedAttempts`, `Auth__LockoutMinutes` | Login lockout per email | `5`, `15` |
| `RateLimiting__GlobalPerMinute`, `RateLimiting__LoginPerMinute` | Rate limits per IP | `100`, `20` |
| `CBL_TEST_SQL` | SQL Server connection string that enables the integration tests | – |
| `CBL_TEST_INMEMORY` | `1` runs the HTTP integration tests against EF InMemory (developer shortcut) | – |

### API at a glance
Contract: [`specs/001-gestion-prestamos/contracts/openapi.yaml`](specs/001-gestion-prestamos/contracts/openapi.yaml). All errors are `application/problem+json`.

| Endpoint | Role | Purpose |
|---|---|---|
| `POST /api/auth/login` | anonymous | Sign in (JWT, 10 minutes, no refresh token) |
| `POST /api/loans` · `GET /api/loans/mine` | Borrower | Request a loan · list my loans |
| `PUT /api/loans/{id}` | Borrower (owner) | Edit while `Pending` |
| `GET /api/loans/{id}` · `GET /api/loans/{id}/status` | owner or Admin | Detail · cached status |
| `DELETE /api/loans/{id}` | owner or Admin | Delete while `Pending` |
| `GET /api/loans?status=` | Admin | All loans (filter by status, no paging) |
| `POST /api/loans/{id}/approve` · `/reject` | Admin | Final decision, audited, `409` if already decided or concurrent |

### Database
The schema lives in `db/CBL.Database` (source of truth; EF Core only maps it, there are no migrations). Build it and apply the `.dacpac` with SqlPackage:

```powershell
dotnet build db/CBL.Database -c Release
sqlpackage /Action:Publish /SourceFile:db/CBL.Database/bin/Release/CBL.Database.dacpac `
  /TargetConnectionString:"<connection string>"
```
The post-deployment script inserts the sample users and the default amount limits (1,000–100,000); it is idempotent and never overwrites later changes.

## Tests
- **Unit tests** (`tests/CBL.UnitTests`): domain, validators and use cases, with EF InMemory. They run everywhere, with no Docker.
- **Integration tests** (`tests/CBL.IntegrationTests`): the real API (`WebApplicationFactory`) against a real SQL Server. They run **only in CI** (a SQL Server service container with the schema deployed from the `.dacpac`). Locally they are **skipped** unless `CBL_TEST_SQL` is defined, so a plain `dotnet test` is green without Docker ([ADR-0021](docs/decisions/0021-integration-tests-ci-only.md)). They cover authentication, authorization, user isolation, decisions, the concurrency race, cache, error format and that the EF mapping matches the `.sqlproj`.
- Developer shortcut: `CBL_TEST_INMEMORY=1` runs the HTTP tests against EF InMemory (the SQL-only tests stay skipped). With any SQL Server, including LocalDB 2019 (add `/p:AllowIncompatiblePlatform=True` to SqlPackage because the project targets SQL Server 2022), set `CBL_TEST_SQL` after publishing the schema.

## In-memory components (single instance)
The loan-status cache, the login lockout counter and the revoked-token list live in the memory of the process: they are lost on restart and not shared between instances. This is acceptable for the scope of the test (one instance, tokens of 10 minutes). Do not enable `InvariantGlobalization`: `Microsoft.Data.SqlClient` does not support it.

## How Spec Kit is used

This project is developed with [GitHub Spec Kit](https://github.com/github/spec-kit) (spec-driven development), with AI assistance. Spec Kit is initialized for two agents (Claude Code and GitHub Copilot); Claude Code drives the phases and Copilot is only a reviewer.

> **Note:** the Spec Kit artifacts (constitution, spec, plan, tasks) are written in **Spanish**; the prompts are kept in [docs/speckit-log.md](docs/speckit-log.md). Docs are bilingual (see [ADR-0022](docs/decisions/0022-docs-and-repo-conventions.md)). In this Spec Kit version the commands are hyphenated skills (`/speckit-specify`, …).

| Phase | Command | Artifact | Status |
|---|---|---|---|
| 0 | Manual analysis | docs/00-analysis.md | Done |
| 1 | /speckit-constitution | .specify/memory/constitution.md | Done |
| 2 | /speckit-specify | specs/001-gestion-prestamos/spec.md | Done |
| 3 | /speckit-clarify | spec.md (Clarifications section) | Done |
| 4 | /speckit-plan | plan.md, research.md, data-model.md, contracts/, quickstart.md | Done |
| 5 | /speckit-tasks | tasks.md | Done |
| 6 | /speckit-analyze | docs/speckit-log.md | Done |
| 7 | /speckit-implement | code | Done (113 of 113 tasks) |

## CI/CD
- **CI (GitHub Actions)**: [`.github/workflows/ci.yml`](.github/workflows/ci.yml) builds (including the `.sqlproj` → `.dacpac`), runs the unit tests, deploys the schema to a SQL Server 2022 service container with `SqlPackage`, runs the integration tests (and fails if any is skipped) and publishes the `.dacpac` as an artifact ([ADR-0016](docs/decisions/0016-ci-pipeline.md)).
- **CD = artifacts only**: pushing a `v*.*.*` tag runs [`.github/workflows/release.yml`](.github/workflows/release.yml), which rebuilds, runs the unit tests and creates a GitHub Release with the `.dacpac`, the API publish zip and `SHA256SUMS.txt`. No cloud deployment ([ADR-0024](docs/decisions/0024-cd-publishes-artifacts-only.md)). The `.dacpac` is applied manually with `SqlPackage` and the API runs with `dotnet CBL.Api.dll`.
- The OpenAPI document generated by the API is exported as a CI artifact and a test checks that it matches [`contracts/openapi.yaml`](specs/001-gestion-prestamos/contracts/openapi.yaml).

## Documentation
- Analysis: [docs/00-analysis.md](docs/00-analysis.md)
- Theory part: ["Theory part" section of this README](#theory-part)
- Spec Kit log (exact prompts per phase): [docs/speckit-log.md](docs/speckit-log.md)
- Decisions with discarded alternatives: [docs/decisions](docs/decisions/README.md)
- Planning: [context](docs/planning/CONTEXT.md) · [backend tasks](docs/planning/tasks-backend.md) · [DevOps tasks](docs/planning/tasks-devops.md)
