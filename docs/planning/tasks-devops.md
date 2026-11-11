# Tasks: Repository setup, CI/CD and Spec Kit workflow (CBL)
Tareas: repositorios, CI/CD y flujo de Spec Kit (backend `cbl-backend` + frontend `cbl-frontend`).

Conventions: `[P]` = can run in parallel with its siblings. `Dep` = must be done first. `Check` = acceptance check.
Prefix `BE` = cbl-backend, `FE` = cbl-frontend, `X` = both repos (do it twice, same steps).
Scope reminder: technical test, no enterprise extras. Only GITHUB_TOKEN is used, no repo secrets, no cloud deployment.

## 0. Naming and layout decisions / Nombres y estructura

| Item | Value |
|---|---|
| Repos | `cbl-backend`, `cbl-frontend` (public, as the PDF asks for a public GitHub repo) |
| Default branch | `main`, trunk-based, short-lived branches, merge by PR (squash or `--no-ff`, see ADR-0019) |
| .NET solution | `CBL.sln` at root; projects `src/CBL.Domain`, `CBL.Application`, `CBL.Infrastructure`, `CBL.Api`; `tests/CBL.UnitTests`, `CBL.IntegrationTests`; `db/CBL.Database` (`.sqlproj`, Microsoft.Build.Sql) |
| Namespaces | `CBL.Domain`, `CBL.Application`, `CBL.Infrastructure`, `CBL.Api` (root namespace = project name) |
| Angular | project name `cbl-frontend`, selector prefix `cbl` |
| Spec folders | backend `specs/001-loan-management/`; frontend `specs/001-web-ui/` (renamed from `001-loans-ui`) |
| Artifact names | `CBL.Database-<ver>.dacpac`, `cbl-api-<ver>.zip`, `cbl-web-<ver>.zip`, plus `SHA256SUMS.txt` |
| Env var contract | `CBL_TEST_SQL` (SQL Server connection string; integration tests skip when unset), `Jwt__Key` etc. via env |
| Domain words | `Loan`, `LoanStatus`, `Loans` table stay. Only the product prefix (`Loans.*`, `loans-*`) becomes `CBL`. |

## 1. Repository creation and branching / Creación de repos y ramas

- [ ] **D1.1 [P] X** Create the two empty public GitHub repos (no README/license/.gitignore from GitHub, to avoid an unrelated-history merge). Clone both locally.
  Check: `git clone` works, `git log` is empty, default branch is `main`.
- [ ] **D1.2 [P] BE** Seed `.gitignore` from the existing `loans-backend/.gitignore`; add `.claude/settings.local.json`, `*.dacpac` stays ignored, `publish/`, `artifacts/`. Add `.editorconfig` and `global.json` (pin .NET 8 SDK, `rollForward: latestFeature`).
  Dep: D1.1. Check: `dotnet --version` resolves to 8.x inside the repo.
- [ ] **D1.3 [P] FE** Seed `.gitignore` from the existing frontend one; add `.claude/settings.local.json`, `dist/`, `.angular/`. Add `.nvmrc` (Node 22 LTS; confirm it is in the `engines` of the installed Angular 19 CLI) and `.editorconfig`.
  Dep: D1.1. Check: `node -v` matches `.nvmrc`.
- [ ] **D1.4 X** Add LICENSE (MIT, unless the user says otherwise) and an initial commit `chore: initialize repository`, pushed straight to `main`. Branch protection is turned on later (D6.1), so bootstrap commits do not need PRs.
  Dep: D1.2/D1.3. Check: `main` exists on GitHub.
- [ ] **D1.5 X** Branching rule written in `CONTRIBUTING.md`-style section of the README (no separate file): Spec Kit feature branches are named `NNN-short-name` (Spec Kit's scripts require this pattern on `/speckit.specify`, `plan`, `tasks`, `implement`); infra/docs branches use `ci/…`, `docs/…`, `chore/…` and must not run Spec Kit commands.
  Check: rule is in README (EN + ES).

## 2. Spec Kit initialization for two agents / `specify init` con dos agentes

Tooling prerequisites (local): Python 3.11+, `uv`, Git, Claude Code, VS Code with GitHub Copilot. Pin the Spec Kit release so both repos use the same templates:
`uvx --from git+https://github.com/github/spec-kit.git@<tag> specify init ...`. Record the `<tag>` in `docs/speckit-log.md`.

- [ ] **D2.1 X** Run `specify init . --ai claude` in the repo root (here-mode over the already seeded folder; accept the "non-empty directory" prompt or add `--force`). Use `--no-git` if the tool tries to re-init Git. Do NOT run `/speckit.*` yet.
  Dep: D1.4. Check: `.specify/` and `.claude/commands/` exist.
- [ ] **D2.2 X** Run `specify init . --ai copilot --force` in the same folder. Run it **before** writing the constitution: `--force` may overwrite template files such as `.specify/memory/constitution.md`.
  Dep: D2.1. Check: `git status` shows only new Copilot files plus harmless re-writes of identical `.specify/` files (`git diff --stat` on `.specify/` is empty or trivial).
- [ ] **D2.3 X** Review and commit exactly what init added, as one commit `chore(spec): init spec kit for claude and copilot`.
  Dep: D2.2. Check: `git show --stat` matches the table below; nothing under `.claude/settings.local.json`.

What init adds (the exact layout depends on the Spec Kit version, so verify with `git status` after D2.2 and trust that over this table):

| Path | Added by | Purpose | Commit it? |
|---|---|---|---|
| `.specify/memory/constitution.md` | both (template) | project principles, filled by `/speckit.constitution` | yes |
| `.specify/templates/*` (spec, plan, tasks, checklist, agent templates) | both | templates the commands fill | yes |
| `.specify/scripts/<bash or powershell>/*` | both | branch creation, prerequisite checks, agent-context update | yes |
| `.claude/commands/speckit.*.md` | claude | slash commands for Claude Code | yes |
| `.github/agents/speckit.*.agent.md` and `.github/prompts/speckit.*.prompt.md` | copilot | Copilot Chat agents/prompt files | yes |
| `.vscode/settings.json` | copilot (if generated) | prompt-file recommendations | yes (add `!.vscode/settings.json` to `.gitignore`, which currently ignores `.vscode/*`) |
| `CLAUDE.md`, `.github/copilot-instructions.md` | created/updated later by the `/speckit.plan` context-update script | agent context with stack and commands | yes (review for secrets and for the Loans name) |
| `specs/NNN-name/…` | `/speckit.specify` onward | feature artifacts | yes |

Both agents share the same `.specify/` and `specs/`; the only duplicated content is the thin per-agent command wrappers.
Rule: **one agent drives each phase** and the log records which (see Open question 3).

## 3. Rename existing docs from Loans to CBL and make them bilingual / Renombrado y documentación bilingüe

Policy (new ADR-0022): English is the default file, Spanish is the `.es.md` twin. Link both ways at the top of each file (`English | [Español](README.es.md)`).
The existing docs are Spanish-only today, so: `git mv X.md X.es.md`, apply the rename in the `.es.md`, then write the English `X.md`.
Do not blind-`sed`: rename only the product prefix. Search patterns: `Loans\.` (project names), `loans-backend`, `loans-frontend`, `Loans Backend|Frontend` (titles), `001-loans-ui`/`loans-ui`. Leave `Loan`, `LoanStatus`, `Loans` table, and the frontend `loans` slice/feature folder alone.

| Existing file | Needs | Rename work |
|---|---|---|
| BE `README.md` | EN + ES | title -> "CBL Backend (Control Banking Loans)"; `Loans.*` -> `CBL.*`; `loans-frontend` -> `cbl-frontend`; status table; add CI badge and "how to run locally / what only runs in CI" |
| BE `docs/00-analisis.md` | EN + ES | §9 structure `Loans.*` -> `CBL.*`; role `Usuario` -> `Borrower` (`Prestamista` in ES); RF-05 roles |
| BE `docs/speckit-log.md` | EN + ES | constitution prompt (roles `Usuario, Admin` -> `Borrower, Admin`; project name; remove Testcontainers mention); phase-4 note (Testcontainers -> service container); the English prompt is what actually gets run (Open question 2) |
| BE `docs/teoria.md` | ES (original) + EN | one `loans` mention only; the 30% theory part is graded, so deliver both; EN may be a faithful translation |
| BE `docs/decisions/README.md` | EN + ES | `loans-frontend` link text; add 0021, 0022 rows |
| BE ADR 0002 | EN + ES | repo names -> `cbl-backend`, `cbl-frontend` |
| BE ADR 0004, 0005 | EN + ES | `Loans.Database` -> `CBL.Database`; `Loans` in 0004 is probably the use-case/table name, check before renaming |
| BE ADR 0009, 0010, 0017, 0018 | EN + ES | role wording `Usuario` -> `Borrower`/`Prestamista` only where it means the role |
| BE ADR 0014 | EN + ES | **do not edit the decision**; mark "Reemplazada por ADR-0021" (Testcontainers needs Docker) |
| BE other ADRs (0001, 0003, 0006-0008, 0011, 0015, 0016, 0019) | EN + ES | no Loans prefix found; only translation (post-MVP) |
| FE `README.md` | EN + ES | title; `001-loans-ui` -> `001-web-ui`; `loans-backend` -> `cbl-backend`; role names |
| FE `docs/00-analisis.md`, `docs/speckit-log.md`, `docs/decisions/README.md` | EN + ES | `loans-backend` -> `cbl-backend`; roles; log prompt for phase 2 points to `cbl-backend` log |
| FE ADRs 0001, 0002, 0012, 0013, 0020 | EN + ES | 0002 repo names; the rest translation only (post-MVP) |
| Spec Kit artifacts (`specs/**`, `.specify/memory/constitution.md`) | **one language only** | generated files are tool input; duplicating them breaks tooling. Spanish summary lives in `docs/*.es.md`. |

Tasks:
- [ ] **D3.1 [P] BE** Copy the 4 backend doc groups into the repo, `git mv` to `.es.md`, apply the rename table above. Commit `docs: rename Loans to CBL (es)`.
  Dep: D2.3. Check: `grep -rniE "Loans\.|loans-(backend|frontend)|Loans (Backend|Frontend)" .` returns nothing; roles read `Borrower`/`Prestamista`.
- [ ] **D3.2 [P] FE** Same for the frontend docs. Check: same grep plus `loans-ui` returns nothing.
  Dep: D2.3.
- [ ] **D3.3 [P] X** Write `README.md` (EN) mirroring the Spanish README: stack, structure, Spec Kit usage table, how to run, CI badge, link to the other repo. Check: every section in `README.md` exists in `README.es.md` and vice versa.
  Dep: D3.1/D3.2.
- [ ] **D3.4 BE** Write English `docs/00-analisis.md` and `docs/speckit-log.md` (prompts translated; these are the ones executed). Check: the speckit-log pair has the same phase headings.
  Dep: D3.1. **MVP**: needed before `/speckit.constitution`.
- [ ] **D3.5 FE** Same for the frontend log. Dep: D3.2. **MVP**.
- [ ] **D3.6 BE** Add `docs/decisions/0021-integration-tests-ci-service-container.md` (EN+ES): SQL Server integration tests use a GitHub Actions service container, run only in CI, skipped locally via env var; supersedes 0014. Add `0022-bilingual-docs-and-repo-conventions.md` (EN+ES): language policy, branch/commit/PR conventions, tag-based artifact release. Update both ADR indexes (BE and FE index rows `0021`, `0022 (see backend repo)`).
  Dep: D3.1. Check: ADR-0014 points to 0021; indexes updated in 4 files.
- [ ] **D3.7 [P] BE/FE** Post-MVP: translate the remaining ADRs (EN or ES twin). Dep: D3.1/D3.2. Not on the critical path.
- [ ] **D3.8 [P] X** Add a tiny docs check to CI (D5.x) that fails when a `*.md` under `docs/` or the root README has no `.es.md` twin. Optional, 10 lines of bash.

Commit: `docs: add english docs and ADR-0021/0022`. Commit pushed straight to `main` (before protection) or via a `docs/…` PR.

## 4. Ordered Spec Kit phases per repo / Fases de Spec Kit y archivos

Each phase = one commit (`docs(spec): <phase>`, ADR-0019). The phase prompt is pasted into `docs/speckit-log.md` (what it generated, what was changed by hand). Paths are relative to repo root.

| # | Command | Where it runs | Files produced or changed |
|---|---|---|---|
| 1 | `/speckit.constitution` | on `main` (no feature branch exists yet) | `.specify/memory/constitution.md` |
| 2 | `/speckit.specify` | creates and switches to branch `001-…` | `specs/001-…/spec.md`, `specs/001-…/checklists/requirements.md` |
| 3 | `/speckit.clarify` | feature branch | `spec.md` (adds a `## Clarifications` section) |
| 4 | `/speckit.plan` | feature branch | `plan.md`, `research.md`, `data-model.md` (BE), `contracts/` (BE: `openapi.yaml`), `quickstart.md`; updates `CLAUDE.md` / `.github/copilot-instructions.md` |
| 5 | `/speckit.tasks` | feature branch | `tasks.md` (task IDs, `[P]` markers, per user story) |
| 6 | `/speckit.analyze` | feature branch | no file (read-only report); paste the result into `docs/speckit-log.md` |
| 7 | `/speckit.implement` | feature branch | source code, tests, workflows; ticks tasks in `tasks.md` |
| opt | `/speckit.checklist` | feature branch | `checklists/<topic>.md`; skip unless time allows |

Feature name for `/speckit.specify`: BE `loan-management` (-> `001-loan-management`), FE `web-ui` (-> `001-web-ui`). Spec Kit derives the branch from the first words of the prompt, so check the branch name and rename it with `git branch -m` before committing if it is not what the table says.

Tasks:
- [ ] **D4.1 BE** `/speckit.constitution` using the English log prompt. Commit `docs(spec): constitution`. Dep: D3.4, D3.6. Check: constitution mentions CBL, Borrower/Admin, `.sqlproj` as source of truth, CI-only SQL Server integration tests; push to `main`.
- [ ] **D4.2 FE** `/speckit.constitution`. Dep: D3.5. [P] with D4.1. Check: mentions token in memory only and the backend OpenAPI as contract.
- [ ] **D4.3 BE** `/speckit.specify` -> `/speckit.clarify` (two commits). Dep: D4.1. Check: spec has no `[NEEDS CLARIFICATION]` left; limits 1,000–100,000 and term 1–60 present.
- [ ] **D4.4 FE** `/speckit.specify` (stories 1-5, 7, plus the admin users screen from the decisions) -> `/speckit.clarify`. Dep: D4.2. [P] with D4.3.
- [ ] **D4.5 BE** `/speckit.plan` -> `/speckit.tasks` -> `/speckit.analyze`. Dep: D4.3. The plan prompt must pin: .NET 8, EF Core + InMemory, `.sqlproj`, FluentValidation, xUnit, `WebApplicationFactory`, **service-container SQL Server in CI (no Testcontainers)**, workflows from section 5. Check: `contracts/openapi.yaml` exists; `tasks.md` has a CI task group that matches section 5.
- [ ] **D4.6 X (handoff)** Copy `specs/001-loan-management/contracts/openapi.yaml` from BE into FE as `specs/001-web-ui/contracts/openapi.yaml` (manual copy, no cross-repo automation). Dep: D4.5 (plan step). Check: FE plan references that file.
- [ ] **D4.7 FE** `/speckit.plan` -> `tasks` -> `analyze`. Dep: D4.4, D4.6. Check: plan pins Angular 19, Angular Material, NgRx, Reactive Forms, API base URL from environment config.
- [ ] **D4.8 X** `/speckit.implement`, in chunks by user story; every commit names the task: `feat(api): approve loan use case (T012)`. Dep: D4.5/D4.7, and CI (section 5) already green on an empty skeleton so each push is verified.

Process rules: after each phase, run D6.4 (secret scan by eye: `git diff --cached`) and update the Status column of the README table.

## 5. GitHub Actions / Workflows

Shared rules: `permissions: contents: read` at workflow level; `concurrency` group per ref with `cancel-in-progress: true`; actions pinned to a major (`actions/checkout@v4`, `setup-dotnet@v4`, `setup-node@v4`, `upload-artifact@v4`, `download-artifact@v4`); triggers `pull_request` and `push` on `main`, plus `workflow_dispatch`. No secrets besides `GITHUB_TOKEN`.

### 5.1 Backend `.github/workflows/ci.yml`

- [ ] **D5.1 BE** Job `build-and-unit` (ubuntu-latest):
  1. checkout, `setup-dotnet` 8.0.x with NuGet cache (`cache: true`, `cache-dependency-path: '**/packages.lock.json'` only if lock files are used, else omit)
  2. `dotnet restore CBL.sln`
  3. `dotnet build CBL.sln -c Release --no-restore` (this also builds `db/CBL.Database` -> `.dacpac`)
  4. `dotnet test tests/CBL.UnitTests -c Release --no-build --logger trx` filtering out `Category=Integration`
  5. `upload-artifact` `dacpac` (`db/CBL.Database/bin/Release/**/CBL.Database.dacpac`), plus test results
  Dep: first compilable skeleton from `/speckit.implement` (stub solution is enough). Check: a PR shows the job green and the artifact is downloadable.
- [ ] **D5.2 BE** Job `integration` (needs `build-and-unit`, ubuntu-latest) with service container:
  - `services.mssql`: `image: mcr.microsoft.com/mssql/server:2022-latest`, `env: ACCEPT_EULA: Y, MSSQL_SA_PASSWORD: <throwaway test password defined once in workflow env>`, `ports: 1433:1433`, `options:` health check running `/opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -Q "SELECT 1"` with `--health-interval 10s --health-timeout 5s --health-retries 12 --health-start-period 20s`
  - steps: download `dacpac` artifact; `dotnet tool install -g microsoft.sqlpackage`; `sqlpackage /Action:Publish /SourceFile:CBL.Database.dacpac /TargetConnectionString:"Server=localhost,1433;Database=CblTest;User Id=sa;Password=...;TrustServerCertificate=True"` (post-deployment seed runs here); set `CBL_TEST_SQL` and a throwaway `Jwt__Key` in `env`; `dotnet test tests/CBL.IntegrationTests -c Release` (or `--filter Category=Integration`)
  - on `main` only: `sqlpackage /Action:DeployReport` against the same container, upload the XML as artifact (ADR-0016)
  Dep: D5.1, integration test project existing. Check: job log shows tests running (not skipped) and covers the EF-vs-`.sqlproj` mapping test; failing the schema deliberately fails the job.
- [ ] **D5.3 BE** Skip mechanism in the test project: a custom `[IntegrationFact]` attribute (sets `Skip` when `CBL_TEST_SQL` is unset) or `[Trait("Category","Integration")]`. Both: trait for filtering, attribute so a plain `dotnet test` stays green without Docker.
  Check: locally `dotnet test` passes and reports the integration tests as **Skipped**, not failed.

### 5.2 Frontend `.github/workflows/ci.yml`

- [ ] **D5.4 FE** Job `lint-test-build` (ubuntu-latest): checkout; `setup-node` from `.nvmrc` with `cache: npm`; `npm ci`; `npm run lint` (angular-eslint; add via `ng add @angular-eslint/schematics` during implement); `npm test -- --watch=false --browsers=ChromeHeadlessCI` (custom launcher with `--no-sandbox` in `karma.conf`; if the default runner differs in the generated project, adapt); `npm run build -- --configuration production`; `upload-artifact` `web-dist` from `dist/cbl-frontend/browser`.
  Dep: Angular skeleton. Check: PR job green; artifact contains `index.html`.
- [ ] **D5.5 FE** Add `lint`, `test:ci` and `build:prod` scripts to `package.json` so CI and local use the same commands. Check: the three run locally.

### 5.3 CD = release artifacts on tag (no deployment)

- [ ] **D5.6 BE** `.github/workflows/release.yml`, trigger `push: tags: ['v*.*.*']`, `permissions: contents: write` on this workflow only. Steps: checkout (full history), verify the tagged commit is reachable from `main` (`git merge-base --is-ancestor $GITHUB_SHA origin/main`), rebuild and run unit tests (do not reuse artifacts from another run), `dotnet publish src/CBL.Api -c Release -o publish && zip` -> `cbl-api-<tag>.zip`, copy dacpac -> `CBL.Database-<tag>.dacpac`, `sha256sum` -> `SHA256SUMS.txt`, then `gh release create "$GITHUB_REF_NAME" --generate-notes <files>` using `GITHUB_TOKEN`.
  Dep: D5.1. Check: pushing tag `v0.1.0` creates a GitHub Release with 3 assets.
- [ ] **D5.7 FE** same pattern: tag `v*.*.*` -> lint/test/build -> zip `dist/cbl-frontend/browser` as `cbl-web-<tag>.zip` + checksum -> GitHub Release. Check: Release with 2 assets.
- [ ] **D5.8 X** Release notes: the README "Releases" section states that artifacts are published only (`.dacpac` is applied manually with `SqlPackage`, API zip is runnable with `dotnet CBL.Api.dll`, web zip is static files). No environment deployment.

## 6. What runs where (no Docker locally) / Qué corre dónde

| Step | Local (no Docker) | CI only |
|---|---|---|
| `dotnet build` including `.sqlproj` -> `.dacpac` | yes (Microsoft.Build.Sql builds with the SDK, no SQL Server needed) | also |
| Unit tests (xUnit + EF InMemory) | yes: `dotnet test --filter "Category!=Integration"` | also |
| App run locally (EF InMemory, Swagger in Development) | yes | no |
| Integration tests (`WebApplicationFactory` + real SQL Server) | **skipped** (no `CBL_TEST_SQL`) | **yes**, service container |
| `sqlpackage` publish, seed post-deployment script, `DeployReport` | no (needs SQL Server) | yes |
| Race test (two concurrent approvals, 409) and EF-vs-`.sqlproj` mapping test | no | yes (rely on CI as the proof) |
| Frontend lint / test / build | yes (Node only) | also |
| Release assets on tag | no | yes |
| Workflow syntax check | `actionlint` (single binary, no Docker; **not** `act`, which needs Docker) | yes (run itself) |

- [ ] **D6.0 [P] X** Optional `scripts/ci-local.sh` (and `.ps1` if the dev machine is Windows) that mirrors the local column: BE restore+build+unit tests; FE `npm ci && lint && test:ci && build:prod`. Referenced from the README. Check: running it matches what CI does for the non-Docker steps.

Because SQL Server code paths are proven only in CI, the first integration-test run is a CI run: open a draft PR early (right after D5.2) so feedback arrives while implementing, and expect a couple of iterations on the service-container health check and connection string.

## 7. Governance: protection, PR template, commits

- [ ] **D6.1 X** Branch protection (classic rule or ruleset) on `main`, enabled **after** the bootstrap commits and the constitution are pushed (Spec Kit's constitution step runs on `main`): require a PR before merging, require status checks (BE: `build-and-unit`, `integration`; FE: `lint-test-build`) and branch up to date, block force-push and deletion, no required approvals (solo developer cannot approve own PRs), allow admin bypass so a CI outage does not lock the repo. No "require signed commits" (not needed for the test).
  Dep: D5.1/D5.2/D5.4 have run at least once (GitHub only lists checks that have run). Check: a direct push to `main` is rejected; a PR with a failing job cannot be merged.
- [ ] **D6.2 X** `.github/pull_request_template.md` (English, short): Summary; Spec/tasks references (`specs/00N-…/tasks.md` IDs); Checklist: build and unit tests pass, integration tests pass in CI, docs updated in EN and `.es.md`, no secrets or real credentials, OpenAPI contract changed? (BE) / copied to FE? Check: opening a PR pre-fills the template.
- [ ] **D6.3 X** Commit conventions in the README/ADR-0022 (no commitlint, over-engineering): Conventional Commits `type(scope): subject`, imperative, <= 72 chars. Types: `feat fix docs test ci build chore refactor`. Scopes: BE `domain application infra api db tests spec`; FE `auth loans admin store ui tests spec`. Spec phases: `docs(spec): constitution|specify|clarify|plan|tasks|analyze`. Implementation commits cite the task ID `(T012)`. One logical change per commit; history is not rewritten before delivery (ADR-0019). Trailer policy handled elsewhere.
- [ ] **D6.4 X** Hygiene before every push: `.gitignore` covers `appsettings.Development.json`, `.env`, `secrets.json`; JWT key and test SA password only come from env (CI uses a throwaway value, not a repo secret); review `git diff --cached` of generated Spec Kit and agent files for pasted secrets.
- [ ] **D6.5 [P] X** Post-MVP: Dependabot (`nuget`, `npm`, `github-actions`, weekly) and `CODEOWNERS`. Skip if short on time.

## 8. Dependency order and MVP cut line / Orden y corte de MVP

```
D1.1 -> D1.2/D1.3 [P] -> D1.4 -> D1.5
D1.4 -> D2.1 -> D2.2 -> D2.3
D2.3 -> D3.1 / D3.2 [P] -> D3.3 [P], D3.4/D3.5 [P], D3.6
D3.4+D3.6 -> D4.1 ; D3.5 -> D4.2 ; (D4.1 || D4.2)
D4.1 -> D4.3 -> D4.5 -> D4.6 -> D4.7 (FE plan) ; D4.2 -> D4.4 -> D4.7
D4.5/D4.7 -> D5.1..D5.5 (CI skeleton green) -> D6.1 -> D4.8 implement
D5.1/D5.4 -> D5.6/D5.7 (release) -> final tag v1.0.0
```

**MVP (must be done before submitting):** D1.1-D1.5, D2.1-D2.3, D3.1-D3.6, D4.1-D4.8, D5.1-D5.5 (CI incl. SQL Server integration job), D5.3 skip mechanism, D6.1 (basic protection), D6.2, D6.3.
-------------------- **MVP CUT LINE** --------------------
**After MVP (nice to have, in this order):** D5.6/D5.7 release-on-tag (small, high visible value: do it before delivery if 30 min remain), D6.0 local script, D3.8 docs-twin check, D3.7 remaining ADR translations, D6.5 Dependabot/CODEOWNERS.

## Open questions for the user

1. **GitHub owner and visibility.** Which account/org hosts `cbl-backend` and `cbl-frontend`? Recommended default: your personal account, both public (the PDF asks for a public repo; branch protection is free only on public repos for personal accounts).
2. **Language of the Spec Kit artifacts** (constitution, spec, plan, tasks). Recommended default: English (code, agent templates and tooling are English; Spanish lives in `docs/*.es.md` and the Spanish prompts stay in `speckit-log.es.md`). Say so if the reviewers expect Spanish specs.
3. **Which agent drives the phases?** Recommended default: Claude Code runs all phases and implementation; Copilot files are installed (as requested) and Copilot is used only as a reviewer or second opinion, with the log noting which agent ran each phase.
4. **Your local OS/shell.** Spec Kit scripts (and `ci-local`) come in bash and PowerShell flavours. Recommended default: let `specify init` auto-detect, and provide `ci-local.sh` only (add `.ps1` if you are on Windows without WSL).

> **Nota / Note (2026-11-11):** Los artefactos de Spec Kit se redactan en español y `docs/speckit-log.md` es un solo archivo bilingüe (ADR-0022); esto reemplaza lo que este plan dice en D3.4, D4.1 y la pregunta abierta 2. / Spec Kit artifacts are written in Spanish and `docs/speckit-log.md` is a single bilingual file (ADR-0022); this supersedes D3.4, D4.1 and open question 2 in this plan.
