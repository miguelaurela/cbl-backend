🇪🇸 [Español](README.es.md)

# CBL Project (Control Banking Loans) – Backend

REST API in ASP.NET Core for the Makers technical test (.NET + Angular version).
The frontend lives in a separate repository: `cbl-frontend`.

## Stack
.NET 8 · ASP.NET Core Web API · EF Core · JWT · FluentValidation · xUnit · SQL Server (`.sqlproj` project)

## Structure
```
/src      CBL.Domain · CBL.Application · CBL.Infrastructure · CBL.Api
/tests    CBL.UnitTests · CBL.IntegrationTests
/db       CBL.Database (SQL project, source of truth for the schema)
/docs     analysis, Spec Kit log, theory, planning and ADRs
/specs    Spec Kit artifacts
/.specify Spec Kit constitution, templates and scripts
```
> Current state: analysis and planning phase completed. The code will be generated with `/speckit.implement`.

## How Spec Kit is used

This project is developed with [GitHub Spec Kit](https://github.com/github/spec-kit) (spec-driven development), with AI assistance. Spec Kit is initialized for two agents (Claude Code and GitHub Copilot); Claude Code drives the phases and Copilot is only a reviewer.

> **Note:** the Spec Kit artifacts (constitution, spec, plan, tasks) are written in **Spanish**; the prompts are kept in [docs/speckit-log.md](docs/speckit-log.md). Docs are bilingual (see [ADR-0022](docs/decisions/0022-docs-and-repo-conventions.md)).

| Phase | Command | Artifact | Status |
|---|---|---|---|
| 0 | Manual analysis | docs/00-analysis.md | Done |
| 1 | /speckit.constitution | .specify/memory/constitution.md | Pending |
| 2 | /speckit.specify | specs/001-loan-management/spec.md | Pending |
| 3 | /speckit.clarify | spec.md (Clarifications section) | Pending |
| 4 | /speckit.plan | plan.md, data-model.md, contracts/ | Pending |
| 5 | /speckit.tasks | tasks.md | Pending |
| 6 | /speckit.analyze | docs/speckit-log.md | Pending |
| 7 | /speckit.implement | code | Pending |

Update the "Status" column as each phase is executed.

## CI/CD
- **CI (GitHub Actions)**: build (including the `.sqlproj` → `.dacpac`), unit tests, and integration tests against a SQL Server service container deployed with `SqlPackage`. Integration tests run **only in CI**; locally they are skipped unless `CBL_TEST_SQL` is set ([ADR-0021](docs/decisions/0021-integration-tests-ci-only.md), [ADR-0016](docs/decisions/0016-ci-pipeline.md)).
- **CD = artifacts only**: pushing a `v*.*.*` tag creates a GitHub Release with the `.dacpac`, the API publish zip and checksums. No cloud deployment ([ADR-0024](docs/decisions/0024-cd-publishes-artifacts-only.md)).

## How to run
TODO: to be written once the implementation exists (run locally with EF InMemory, run unit tests, how to apply the `.dacpac`, environment variables such as `Jwt__Key` and `CBL_TEST_SQL`).

## Documentation
- Analysis: [docs/00-analysis.md](docs/00-analysis.md)
- Theory part: [docs/theory.md](docs/theory.md)
- Spec Kit log (exact prompts per phase): [docs/speckit-log.md](docs/speckit-log.md)
- Decisions with discarded alternatives: [docs/decisions](docs/decisions/README.md)
- Planning: [context](docs/planning/CONTEXT.md) · [backend tasks](docs/planning/tasks-backend.md) · [DevOps tasks](docs/planning/tasks-devops.md)
