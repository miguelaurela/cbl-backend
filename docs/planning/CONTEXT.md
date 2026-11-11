# CBL Project (Control Banking Loans) – shared context for planning agents

Technical test (Makers), originally Java/Spring Boot + Angular, solved in **.NET 8 + Angular 19**. Two repos: `cbl-backend`, `cbl-frontend`. Spec Kit is used (agents: Claude Code and GitHub Copilot). It is a technical test: **keep scope to what the PDF asks; no enterprise-level over-engineering.** Estimated 2 h of work in the PDF, so prioritize an MVP.
Code in English; documentation bilingual (English and Spanish). Existing docs: /home/claude/loans-backend/docs and /home/claude/loans-frontend/docs (read 00-analisis.md and decisions/ in each; names there say "Loans"/"loans-*" and will be renamed to CBL; ignore that).

## PDF requirements (summary)
- Loan management API: request loan (amount + term in months), admin approves/rejects, user queries status, CRUD loans and users with business rules.
- Clean/hexagonal architecture, JWT with roles, cache on loan status queries (invalidate on decision), transactional/consistent approval, unit tests of critical operations, clear HTTP errors.
- Angular: JWT + guards, Reactive Forms, state via NgRx (decided), 3 screens (Login, user/borrower view, admin view), good styling (Angular Material decided).
- Theory part: 6 questions (draft in loans-backend/docs/teoria.md).

## Decisions already made
- .NET 8 LTS; Angular 19 + Angular Material + NgRx; Reactive Forms.
- Roles: exactly two: **Admin** (approves/rejects, manages users incl. UI screen for users, manages loan-amount limits) and **Borrower** (Spanish: "Prestamista"; requests loans and sees only their own). Code uses `Admin` and `Borrower`.
- Clean architecture: Domain, Application, Infrastructure, Api; light CQRS by convention, no MediatR.
- DB: SQL Server schema defined in a `.sqlproj` (Microsoft.Build.Sql) = source of truth, no EF migrations; EF Core maps via OnModelCreating. Seed in post-deployment script (hashed passwords for usuario@test.com / admin@test.com, password `123`, limits 1,000–100,000 in `SystemParameters`).
- EF InMemory for local runs and unit tests. **The user has NO Docker locally**: SQL Server integration tests (WebApplicationFactory + SQL Server) run **only in GitHub Actions** (service container), and locally they must be skippable (category/trait or env var).
- Optimistic concurrency (version column) → 409; atomic approval + audit; cache `IMemoryCache` read side, invalidate after save.
- JWT lifetime **10 minutes**, no refresh token, token kept only in memory in the frontend; login lockout after repeated failures (e.g. 5 attempts / 15 min, configurable); rate limiting; security headers; CORS to frontend origin; ProblemDetails errors; Swagger only in Development.
- Term 1–60 months; statuses Pending/Approved/Rejected; decision is final; audit of who/when.
- CI: GitHub Actions. **CD = publish artifacts only** (GitHub Releases / artifacts: `.dacpac`, API publish zip, Angular production build); no cloud deployment.
- Spec Kit artifacts (.specify/, specs/) versioned in the repos; bitácora docs/speckit-log.md; ADRs.

## Your output
Write ONLY the file you are told to write (a tasks document in English with a short Spanish summary line per section header is fine). Make tasks small, ordered, with IDs, dependencies, a [P] marker for tasks that can run in parallel, acceptance check per task, and a clear MVP cut line. Keep it concise (technical-test scale). At the end of your file add a section "## Open questions for the user" listing ONLY decisions that truly need the user's input (max 5, each with a recommended default). Reply to me with a 5-line summary and that same list of open questions.
