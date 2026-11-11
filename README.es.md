🇬🇧 [English](README.md)

# CBL Project (Control Banking Loans) – Backend

API REST en ASP.NET Core para la prueba técnica de Makers (versión .NET + Angular).
El frontend vive en un repositorio separado: `cbl-frontend`.

## Stack
.NET 8 · ASP.NET Core Web API · EF Core · JWT · FluentValidation · xUnit · SQL Server (proyecto `.sqlproj`)

## Estructura
```
/src      CBL.Domain · CBL.Application · CBL.Infrastructure · CBL.Api
/tests    CBL.UnitTests · CBL.IntegrationTests
/db       CBL.Database (proyecto SQL, fuente de verdad del esquema)
/docs     análisis, bitácora de Spec Kit, teoría, planificación y ADRs
/specs    artefactos de Spec Kit
/.specify constitution, plantillas y scripts de Spec Kit
```
> Estado actual: fases de análisis y planificación completadas. El código se generará con `/speckit.implement`.

## Cómo se usa Spec Kit

Este proyecto se desarrolla con [GitHub Spec Kit](https://github.com/github/spec-kit) (desarrollo guiado por especificaciones), con asistencia de IA. Spec Kit está inicializado para dos agentes (Claude Code y GitHub Copilot); Claude Code conduce las fases y Copilot solo revisa.

> **Nota:** los artefactos de Spec Kit (constitution, spec, plan, tasks) están redactados en **español**; los prompts se guardan en [docs/speckit-log.md](docs/speckit-log.md). La documentación es bilingüe (ver [ADR-0022](docs/decisions/0022-docs-and-repo-conventions.md)).

| Fase | Comando | Artefacto | Estado |
|---|---|---|---|
| 0 | Análisis manual | docs/00-analisis.es.md | Hecho |
| 1 | /speckit.constitution | .specify/memory/constitution.md | Pendiente de ejecutar |
| 2 | /speckit.specify | specs/001-loan-management/spec.md | Pendiente de ejecutar |
| 3 | /speckit.clarify | spec.md (sección Clarifications) | Pendiente |
| 4 | /speckit.plan | plan.md, data-model.md, contracts/ | Pendiente |
| 5 | /speckit.tasks | tasks.md | Pendiente |
| 6 | /speckit.analyze | docs/speckit-log.md | Pendiente |
| 7 | /speckit.implement | código | Pendiente |

Actualiza la columna "Estado" a medida que ejecutes cada fase.

## CI/CD
- **CI (GitHub Actions)**: compilación (incluye `.sqlproj` → `.dacpac`), pruebas unitarias y pruebas de integración contra un contenedor de servicio de SQL Server desplegado con `SqlPackage`. Las pruebas de integración corren **solo en CI**; en local se omiten salvo que se defina `CBL_TEST_SQL` ([ADR-0021](docs/decisions/0021-integration-tests-ci-only.md), [ADR-0016](docs/decisions/0016-ci-pipeline.md)).
- **CD = solo artefactos**: al subir un tag `v*.*.*` se crea un GitHub Release con el `.dacpac`, el zip de publicación de la API y los checksums. Sin despliegue en la nube ([ADR-0024](docs/decisions/0024-cd-publishes-artifacts-only.md)).

## Cómo ejecutar
TODO: se redactará cuando exista la implementación (ejecución local con EF InMemory, pruebas unitarias, cómo aplicar el `.dacpac`, variables de entorno como `Jwt__Key` y `CBL_TEST_SQL`).

## Documentación
- Análisis: [docs/00-analisis.es.md](docs/00-analisis.es.md)
- Parte teórica: [docs/teoria.es.md](docs/teoria.es.md)
- Bitácora de Spec Kit (prompts exactos por fase): [docs/speckit-log.md](docs/speckit-log.md)
- Decisiones con alternativas descartadas: [docs/decisions](docs/decisions/README.md)
- Planificación: [contexto](docs/planning/CONTEXT.md) · [tareas backend](docs/planning/tasks-backend.md) · [tareas DevOps](docs/planning/tasks-devops.md)
