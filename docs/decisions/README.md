# Architecture Decision Records (ADR) / Registro de decisiones

🇬🇧 Numbering is shared with the frontend repo. This index lists those that affect the backend; frontend ones (0012, 0013, 0020, 0025) live in `cbl-frontend`. Each ADR is a single bilingual file (English first, then `## Español`).
🇪🇸 La numeración se comparte con el repo de frontend. Aquí están las que afectan al backend; las de frontend (0012, 0013, 0020, 0025) están en `cbl-frontend`. Cada ADR es un único archivo bilingüe (inglés primero, luego `## Español`).

| ADR | English title | Título en español |
|---|---|---|
| [0001](0001-dotnet-angular.md) | Solve the test with .NET + Angular | Resolver la prueba en .NET + Angular |
| [0002](0002-two-repositories.md) | Two repositories | Dos repositorios |
| [0003](0003-clean-architecture.md) | Clean architecture with 4 projects | Arquitectura limpia con 4 proyectos |
| [0004](0004-light-cqrs.md) | Light CQRS without MediatR | CQRS ligero sin MediatR |
| [0005](0005-sqlproj-source-of-truth.md) | `.sqlproj` as the source of truth, no EF migrations | `.sqlproj` como fuente de verdad, sin migraciones de EF |
| [0006](0006-database-strategy.md) | EF InMemory locally; real SQL Server in CI | EF en memoria local; SQL Server real en CI |
| [0007](0007-optimistic-concurrency.md) | Optimistic concurrency with 409 | Concurrencia optimista con 409 |
| [0008](0008-limits-in-database.md) | Loan-amount limits in a parameters table | Límites de monto en tabla de parámetros |
| [0009](0009-short-lived-jwt.md) | Short-lived JWT (10 min), no refresh token | JWT de vida corta (10 min), sin refresh token |
| [0010](0010-security-middleware.md) | Security through middleware | Seguridad por middleware |
| [0011](0011-cache.md) | Read-side cache with `IMemoryCache` | Caché en lectura con `IMemoryCache` |
| 0012 | NgRx (see frontend repo) | NgRx (ver repo frontend) |
| 0013 | Token in memory (see frontend repo) | Token en memoria (ver repo frontend) |
| [0014](0014-integration-tests.md) | Integration tests with SQL Server in a container (superseded by 0021) | Pruebas de integración con SQL Server en contenedor (reemplazada por 0021) |
| [0015](0015-business-rules.md) | Term in months and immutable decisions | Plazo en meses y decisiones inmutables |
| [0016](0016-ci-pipeline.md) | CI pipeline with `.dacpac` deployment | Pipeline de CI con despliegue del `.dacpac` |
| [0017](0017-user-management.md) | User management by the administrator | Gestión de usuarios por el administrador |
| [0018](0018-passwords-and-login.md) | Hashed passwords and login protection | Contraseñas con hash y protección del login |
| [0019](0019-speckit-versioning.md) | Spec Kit versioned in the repository | Spec Kit versionado en el repositorio |
| 0020 | Guards, interceptors and Reactive Forms (see frontend repo) | Guards, interceptores y Reactive Forms (ver repo frontend) |
| [0021](0021-integration-tests-ci-only.md) | Integration tests run in CI only (no local Docker) | Las pruebas de integración corren solo en CI (sin Docker local) |
| [0022](0022-docs-and-repo-conventions.md) | Docs and repo conventions | Convenciones de documentación y repositorios |
| [0023](0023-backend-business-rule-defaults.md) | Backend business-rule defaults | Valores por defecto de reglas de negocio del backend |
| [0024](0024-cd-publishes-artifacts-only.md) | CD publishes artifacts only | El CD solo publica artefactos |
| 0025 | Frontend UI defaults (see frontend repo) | Valores por defecto de la UI del frontend (ver repo frontend) |

🇬🇧 If you change a decision, do not edit the original ADR: create a new one and mark the old one as "Superseded by ADR-00XX".
🇪🇸 Si cambias una decisión, no edites el ADR original: crea uno nuevo y marca el anterior como "Reemplazada por ADR-00XX".
