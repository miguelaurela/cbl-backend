🇬🇧 [English](README.md)

# CBL Project (Control Banking Loans) – Backend

API REST en ASP.NET Core para la prueba técnica de Makers (versión .NET + Angular).
El frontend vive en un repositorio separado: [`cbl-frontend`](https://github.com/miguelaurela/cbl-frontend).

## Stack
.NET 8 · ASP.NET Core Web API · EF Core · JWT · FluentValidation · xUnit · SQL Server (proyecto `.sqlproj`)

## Estructura
```
/src      CBL.Domain · CBL.Application · CBL.Infrastructure · CBL.Api
/tests    CBL.UnitTests · CBL.IntegrationTests
/db       CBL.Database (proyecto SQL, fuente de verdad del esquema)
/docs     análisis, bitácora de Spec Kit, teoría, planificación y ADRs
/specs    artefactos de Spec Kit (specs/001-gestion-prestamos)
/.specify constitution, plantillas y scripts de Spec Kit
```

## Estado actual
La implementación (fase 7 de Spec Kit) está **completa** en `main`: las ocho historias de usuario (login, solicitar un préstamo, mis préstamos y estado, lista del admin, aprobar o rechazar con auditoría y control de concurrencia, gestión de usuarios, límites de monto y cierre de sesión), los workflows de CI y de release y las tareas de pulido están hechas. Las tareas están en [`specs/001-gestion-prestamos/tasks.md`](specs/001-gestion-prestamos/tasks.md) y la validación en vivo del quickstart en [`quickstart-results.md`](specs/001-gestion-prestamos/quickstart-results.md). Lo siguiente es el frontend (`cbl-frontend`), que debe llamar a `POST /api/auth/logout` y conciliarse con este contrato OpenAPI.

## Cómo ejecutar (local, sin Docker ni SQL Server)
Requisitos: SDK de .NET 8 (`global.json` lo fija).

```powershell
dotnet restore
dotnet build
dotnet test                       # pruebas unitarias; las de integración se omiten (ver abajo)

$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:Jwt__Key = "<una clave de desarrollo de al menos 32 caracteres>"
dotnet run --project src/CBL.Api
```

En Development la API usa **EF InMemory** con los datos de ejemplo y sirve Swagger UI en `/swagger` (Swagger nunca se habilita fuera de Development). La clave JWT es **obligatoria** y nunca se sube al repositorio: usa una variable de entorno o `dotnet user-secrets`.

Credenciales de ejemplo (**solo de ejemplo**, documentadas como tales): `usuario@test.com` (Borrower) y `admin@test.com` (Admin), contraseña `123`.

### Variables de entorno
| Variable | Para qué sirve | Valor por defecto |
|---|---|---|
| `Jwt__Key` | Clave de firma del JWT (≥ 32 bytes). **Obligatoria.** | – |
| `Jwt__Issuer`, `Jwt__Audience` | Emisor y audiencia del token | `cbl-api`, `cbl-clients` |
| `Database__Provider` | `InMemory` o `SqlServer` | `InMemory` en Development, `SqlServer` en el resto |
| `ConnectionStrings__Cbl` | Cadena de conexión de SQL Server (si el proveedor es `SqlServer`) | – |
| `Cors__AllowedOrigins__0` | Origen del frontend permitido por CORS | `http://localhost:4200` |
| `Auth__MaxFailedAttempts`, `Auth__LockoutMinutes` | Bloqueo de login por correo | `5`, `15` |
| `RateLimiting__GlobalPerMinute`, `RateLimiting__LoginPerMinute` | Límites de tasa por IP | `100`, `20` |
| `CBL_TEST_SQL` | Cadena de conexión de SQL Server que habilita las pruebas de integración | – |
| `CBL_TEST_INMEMORY` | `1` ejecuta las pruebas HTTP de integración contra EF InMemory (atajo de desarrollo) | – |

### La API de un vistazo
Contrato: [`specs/001-gestion-prestamos/contracts/openapi.yaml`](specs/001-gestion-prestamos/contracts/openapi.yaml). Todos los errores son `application/problem+json`.

| Endpoint | Rol | Para qué |
|---|---|---|
| `POST /api/auth/login` | anónimo | Iniciar sesión (JWT de 10 minutos, sin refresh token) |
| `POST /api/loans` · `GET /api/loans/mine` | Borrower | Solicitar un préstamo · listar los míos |
| `PUT /api/loans/{id}` | Borrower (dueño) | Editar mientras esté `Pending` |
| `GET /api/loans/{id}` · `GET /api/loans/{id}/status` | dueño o Admin | Detalle · estado con caché |
| `DELETE /api/loans/{id}` | dueño o Admin | Eliminar mientras esté `Pending` |
| `GET /api/loans?status=` | Admin | Todos los préstamos (filtro por estado, sin paginación) |
| `POST /api/loans/{id}/approve` · `/reject` | Admin | Decisión final y auditada; `409` si ya está decidido o hay concurrencia |

### Base de datos
El esquema vive en `db/CBL.Database` (fuente de verdad; EF Core solo lo mapea y no hay migraciones). Se compila y se aplica el `.dacpac` con SqlPackage:

```powershell
dotnet build db/CBL.Database -c Release
sqlpackage /Action:Publish /SourceFile:db/CBL.Database/bin/Release/CBL.Database.dacpac `
  /TargetConnectionString:"<cadena de conexión>"
```
El script de post-despliegue inserta los usuarios de ejemplo y los límites de monto por defecto (1.000–100.000); es idempotente y nunca pisa cambios posteriores.

## Pruebas
- **Unitarias** (`tests/CBL.UnitTests`): dominio, validadores y casos de uso, con EF InMemory. Corren en cualquier sitio, sin Docker.
- **De integración** (`tests/CBL.IntegrationTests`): la API real (`WebApplicationFactory`) contra un SQL Server real. Corren **solo en CI** (contenedor de servicio de SQL Server con el esquema desplegado desde el `.dacpac`). En local se **omiten** salvo que se defina `CBL_TEST_SQL`, así que un `dotnet test` normal queda en verde sin Docker ([ADR-0021](docs/decisions/0021-integration-tests-ci-only.md)). Cubren autenticación, autorización, aislamiento entre usuarios, decisiones, la carrera de concurrencia, caché, formato de errores y que el mapeo de EF coincide con el `.sqlproj`.
- Atajo para quien desarrolla: `CBL_TEST_INMEMORY=1` ejecuta las pruebas HTTP contra EF InMemory (las que exigen SQL Server siguen omitidas). Con cualquier SQL Server, incluido LocalDB 2019 (añade `/p:AllowIncompatiblePlatform=True` a SqlPackage porque el proyecto apunta a SQL Server 2022), define `CBL_TEST_SQL` tras publicar el esquema.

## Componentes en memoria (una sola instancia)
La caché del estado de préstamos, el contador de bloqueo de login y la lista de tokens revocados viven en la memoria del proceso: se pierden al reiniciar y no se comparten entre instancias. Es aceptable para el alcance de la prueba (una instancia, tokens de 10 minutos). No actives `InvariantGlobalization`: `Microsoft.Data.SqlClient` no lo soporta.

## Cómo se usa Spec Kit

Este proyecto se desarrolla con [GitHub Spec Kit](https://github.com/github/spec-kit) (desarrollo guiado por especificaciones), con asistencia de IA. Spec Kit está inicializado para dos agentes (Claude Code y GitHub Copilot); Claude Code conduce las fases y Copilot solo revisa.

> **Nota:** los artefactos de Spec Kit (constitution, spec, plan, tasks) están redactados en **español**; los prompts se guardan en [docs/speckit-log.md](docs/speckit-log.md). La documentación es bilingüe (ver [ADR-0022](docs/decisions/0022-docs-and-repo-conventions.md)). En esta versión de Spec Kit los comandos son skills con guion (`/speckit-specify`, …).

| Fase | Comando | Artefacto | Estado |
|---|---|---|---|
| 0 | Análisis manual | docs/00-analisis.es.md | Hecho |
| 1 | /speckit-constitution | .specify/memory/constitution.md | Hecho |
| 2 | /speckit-specify | specs/001-gestion-prestamos/spec.md | Hecho |
| 3 | /speckit-clarify | spec.md (sección Clarifications) | Hecho |
| 4 | /speckit-plan | plan.md, research.md, data-model.md, contracts/, quickstart.md | Hecho |
| 5 | /speckit-tasks | tasks.md | Hecho |
| 6 | /speckit-analyze | docs/speckit-log.md | Hecho |
| 7 | /speckit-implement | código | Hecho (113 de 113 tareas) |

## CI/CD
- **CI (GitHub Actions)**: [`.github/workflows/ci.yml`](.github/workflows/ci.yml) compila (incluye `.sqlproj` → `.dacpac`), ejecuta las pruebas unitarias, despliega el esquema en un contenedor de servicio de SQL Server 2022 con `SqlPackage`, ejecuta las pruebas de integración (y falla si alguna se omite) y publica el `.dacpac` como artefacto ([ADR-0016](docs/decisions/0016-ci-pipeline.md)).
- **CD = solo artefactos**: al subir un tag `v*.*.*` se ejecuta [`.github/workflows/release.yml`](.github/workflows/release.yml), que reconstruye, ejecuta las pruebas unitarias y crea un GitHub Release con el `.dacpac`, el zip de publicación de la API y `SHA256SUMS.txt`. Sin despliegue en la nube ([ADR-0024](docs/decisions/0024-cd-publishes-artifacts-only.md)). El `.dacpac` se aplica a mano con `SqlPackage` y la API se ejecuta con `dotnet CBL.Api.dll`.
- El documento OpenAPI que genera la API se exporta como artefacto de CI y una prueba comprueba que coincide con [`contracts/openapi.yaml`](specs/001-gestion-prestamos/contracts/openapi.yaml).

## Documentación
- Análisis: [docs/00-analisis.es.md](docs/00-analisis.es.md)
- Parte teórica: [docs/teoria.es.md](docs/teoria.es.md)
- Bitácora de Spec Kit (prompts exactos por fase): [docs/speckit-log.md](docs/speckit-log.md)
- Decisiones con alternativas descartadas: [docs/decisions](docs/decisions/README.md)
- Planificación: [contexto](docs/planning/CONTEXT.md) · [tareas backend](docs/planning/tasks-backend.md) · [tareas DevOps](docs/planning/tasks-devops.md)
