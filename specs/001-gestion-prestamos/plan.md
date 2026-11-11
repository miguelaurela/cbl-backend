# Plan de implementación: CBL Project – Gestión de préstamos bancarios (backend)

**Rama**: `001-gestion-prestamos` | **Fecha**: 2026-11-11 | **Especificación**: [spec.md](spec.md)

**Entrada**: especificación de la funcionalidad en `specs/001-gestion-prestamos/spec.md` (31 requisitos funcionales, 11 criterios de éxito, aclaraciones del 2026-11-11).

## Resumen

API REST de gestión de préstamos: un Borrower solicita préstamos y consulta los suyos; un Admin los ve todos, aprueba o rechaza, gestiona usuarios y los límites de monto. Enfoque técnico (detalle en [research.md](research.md)): ASP.NET Core Web API sobre .NET 8 con arquitectura limpia en cuatro proyectos, casos de uso como clases simples (comandos y consultas, sin MediatR), esquema definido en un `.sqlproj` (fuente de verdad) mapeado con EF Core sin migraciones, concurrencia optimista con columna de versión propia (409), caché de lectura con `IMemoryCache`, JWT de 10 minutos con policies por rol y un pipeline de middleware de seguridad. EF InMemory para local y pruebas unitarias; SQL Server real solo en CI para las pruebas de integración.

**Alcance de este plan**: cubre los requisitos del backend (FR-001 a FR-025) y el contrato REST que consume el frontend. Los requisitos de interfaz (FR-026 a FR-031: pantallas, etiquetas en español, USD, rutas protegidas) los implementa `cbl-frontend`; aquí solo se garantiza que la API los soporte.

## Contexto técnico

**Lenguaje/Versión**: C# 12 sobre .NET 8 (LTS). `global.json` fija el SDK 8.0.x (en esta máquina solo hay SDK 6 y 9: hay que instalar el 8 antes de implementar).

**Dependencias principales**: ASP.NET Core 8 (Web API, `Microsoft.AspNetCore.Authentication.JwtBearer`, `RateLimiter`), EF Core 8 (`SqlServer` e `InMemory`), `Microsoft.Extensions.Identity.Core` (`PasswordHasher`, PBKDF2), FluentValidation 11, Swashbuckle.AspNetCore 6 (solo Development), `Microsoft.Build.Sql` (SDK del `.sqlproj`).

**Almacenamiento**: SQL Server (esquema desde `db/CBL.Database` → `.dacpac` → SqlPackage). EF InMemory solo en local y pruebas unitarias; el proveedor se elige con `Database:Provider = InMemory | SqlServer`.

**Pruebas**: xUnit, NSubstitute, FluentAssertions 6.x, `Microsoft.AspNetCore.Mvc.Testing` (`WebApplicationFactory`). Integración solo en CI (contenedor de servicio de SQL Server); en local se omiten salvo que exista `CBL_TEST_SQL`.

**Plataforma de destino**: servidor ASP.NET Core multiplataforma (Kestrel), una sola instancia (la caché y el contador de bloqueo viven en memoria del proceso).

**Tipo de proyecto**: servicio web (API REST) con contrato OpenAPI para el frontend (`cbl-frontend`).

**Metas de rendimiento**: 95 % de las consultas de estado de préstamos en menos de 1 s (SC-006), apoyado en caché con TTL absoluto de 5 minutos e invalidación explícita tras cada decisión.

**Restricciones**: sin Docker local; sin despliegue en la nube (CD solo publica artefactos); secretos fuera del repositorio; Swagger solo en Development; sesión JWT de 10 minutos sin refresh.

**Escala/Alcance**: prueba técnica; decenas de usuarios y cientos de préstamos. Sin paginación (FR-013), sin intereses, cuotas ni pagos.

Sin marcadores `NEEDS CLARIFICATION` pendientes: todas las incógnitas se resuelven en [research.md](research.md).

## Verificación de la constitución

*Puerta: debe pasar antes de la Fase 0 de investigación; se re-evalúa tras el diseño de la Fase 1.*

| Principio | Cómo lo cumple el plan | Previo | Posterior |
|---|---|---|---|
| I. Arquitectura limpia | `CBL.Domain` sin referencias ni paquetes; `CBL.Application` define puertos y casos de uso (comandos/consultas, sin MediatR); `CBL.Infrastructure` implementa con EF Core; `CBL.Api` solo traduce HTTP. Todo el I/O es `async`. | Pasa | Pasa |
| II. `.sqlproj` fuente de verdad | `db/CBL.Database` define tablas, restricciones, índices, columna `Version` y seed; EF solo mapea en `OnModelCreating`, sin migraciones; InMemory solo local/unitarias; concurrencia optimista con 409; aprobación y auditoría en un único `SaveChanges`; límites en `SystemParameters`. | Pasa | Pasa |
| III. Seguridad por defecto | JWT 10 min sin refresh (emisor, audiencia, firma, expiración); policies `AdminOnly`/`BorrowerOnly` y política de respaldo autenticada; id de usuario siempre del token; PBKDF2; bloqueo 5/15 min configurable; pipeline en orden; DTOs de entrada y salida; consultas parametrizadas por EF; Swagger solo Development. | Pasa | Pasa |
| IV. Consistencia y errores | Transiciones validadas en la entidad (`Pending` → `Approved`/`Rejected`, final); reglas de edición y borrado por rol; `ProblemDetails` para todo error con 400/401/403/404/409/422 (más 423 y 429 por bloqueo y límite de tasa). | Pasa | Pasa |
| V. Caché de lectura | `IMemoryCache` tras `IReadCache`, solo en consultas de estado y límites, TTL absoluto 5 min, invalidación después de confirmar el guardado. | Pasa | Pasa |
| VI. Calidad y pruebas | xUnit para dominio, validadores y casos de uso; integración con `WebApplicationFactory` contra SQL Server solo en CI (esquema desde el `.dacpac`, sin Testcontainers); prueba de coincidencia EF ↔ `.sqlproj`. | Pasa | Pasa |
| VII. Especificación primero y MVP | Cada endpoint remite a un FR del spec; se retiraron del borrador los endpoints sin respaldo en el spec (ver desviaciones). Corte de MVP explícito. | Pasa | Pasa |

**Resultado**: sin violaciones; no hace falta la sección de seguimiento de complejidad. La nota de VII (endpoints retirados) se detalla a continuación.

## Desviaciones respecto al borrador `docs/planning/tasks-backend.md`

El borrador se escribió antes de las aclaraciones. El plan lo ajusta así; `/speckit-tasks` usará el borrador solo como base.

| # | Borrador | Plan | Motivo |
|---|---|---|---|
| D1 | Proyectos `Cbl.*` | `CBL.Domain`, `CBL.Application`, `CBL.Infrastructure`, `CBL.Api`, `CBL.UnitTests`, `CBL.IntegrationTests`, `db/CBL.Database` | Nombres de la constitución (principio I). |
| D2 | `GET /api/loans` con `page` y `pageSize` | Sin paginación; filtro opcional `status` | FR-013 y ADR-0025 (aclaración 5). |
| D3 | `GET /api/auth/me`, `GET /api/loans/{id}/audit`, `DELETE /api/users/{id}` | Se omiten | No están en el spec (principio VII). El login devuelve el usuario, la decisión (quién y cuándo) viaja en el préstamo y la desactivación es `PUT /api/users/{id}` con `isActive=false`. |
| D4 | Columnas de bloqueo en `Users`; método de dominio en `User` | Contador de intentos en memoria (`ILoginAttemptTracker`) por correo | Aclaración: el bloqueo cuenta por correo, exista o no (FR-003). Un correo inexistente no tiene fila donde guardar el contador. Contrapartida: el contador se pierde al reiniciar y no se comparte entre instancias (aceptable con una sola instancia; documentado). |
| D5 | Protección del "último admin activo" | No hace falta regla extra | Un admin no puede desactivarse a sí mismo (FR-020), así que siempre queda al menos uno activo. El intento responde **403** (coincide con el contrato que asume el frontend). |
| D6 | `RequestedAt` | `createdAt` | Coincide con el contrato asumido por el frontend. |
| D7 | Cuerpo de aprobar/rechazar | Cuerpo opcional `{ "version": n }`; si llega y no coincide → 409 | Compatible con el frontend; una segunda decisión siempre da 409. |
| D8 | Sin endpoint de cierre de sesión | `POST /api/auth/logout` (204) que revoca el `jti` del token hasta su expiración (lista de revocados en memoria, `ITokenRevocationStore`) | FR-004 y la historia 8 exigen que tras cerrar sesión ya no se usen funciones privadas; con un JWT sin estado, descartar el token en el cliente no basta durante los 10 minutos restantes. **Añade un endpoint que el contrato asumido por el frontend no incluye** (decisión por confirmar). |

## Estructura del proyecto

### Documentación (esta funcionalidad)

```text
specs/001-gestion-prestamos/
├── plan.md              # Este archivo (salida de /speckit-plan)
├── research.md          # Fase 0: decisiones y alternativas
├── data-model.md        # Fase 1: entidades, restricciones, transiciones
├── quickstart.md        # Fase 1: guía de validación de extremo a extremo
├── contracts/
│   └── openapi.yaml     # Fase 1: contrato REST para el frontend
├── checklists/
│   └── requirements.md  # Calidad de la especificación
└── tasks.md             # Fase 2 (/speckit-tasks; NO lo crea /speckit-plan)
```

### Código fuente (raíz del repositorio)

```text
CBL.sln
global.json                      # SDK 8.0.x
Directory.Build.props            # nullable, warnings as errors, implicit usings
src/
├── CBL.Domain/                  # sin dependencias: entidades, enums, excepciones de dominio
│   ├── Users/ (User, Role)
│   ├── Loans/ (Loan, LoanStatus, LoanDecisionAudit)
│   ├── Parameters/ (SystemParameter)
│   └── Exceptions/
├── CBL.Application/             # casos de uso y puertos; sin MediatR
│   ├── Abstractions/ (IClock, ICurrentUser, IPasswordHasher, ITokenService, IReadCache,
│   │                  ILoginAttemptTracker, ITokenRevocationStore, repositorios, IUnitOfWork)
│   ├── Auth/ (Login, Logout)
│   ├── Loans/ (RequestLoan, UpdateLoan, DeleteLoan, ApproveLoan, RejectLoan,
│   │           GetMyLoans, GetAllLoans, GetLoanById, GetLoanStatus)
│   ├── Users/ (CreateUser, GetUsers, GetUserById, UpdateUser)
│   ├── Parameters/ (GetLoanLimits, UpdateLoanLimits)
│   └── Common/ (DTOs, validadores FluentValidation, excepciones de aplicación)
├── CBL.Infrastructure/          # adaptadores: EF Core, caché, JWT, hash, reloj
│   ├── Persistence/ (CblDbContext + OnModelCreating, repositorios, UnitOfWork, seeder InMemory)
│   ├── Security/ (JwtTokenService, PasswordHasherAdapter, LoginAttemptTracker, TokenRevocationStore)
│   ├── Caching/ (MemoryReadCache)
│   └── DependencyInjection.cs
└── CBL.Api/                     # composición y HTTP
    ├── Endpoints/               # grupos de endpoints mínimos; equivalen a los "controladores" del principio I; delgados: request → caso de uso → DTO
    ├── Middleware/ (cabeceras de seguridad, correlation ID, manejo de errores)
    ├── Program.cs
    └── appsettings*.json        # sin secretos
tests/
├── CBL.UnitTests/               # dominio, validadores, casos de uso, mapeo de errores
└── CBL.IntegrationTests/        # WebApplicationFactory + SQL Server (solo CI, [Trait Category=Integration])
db/
└── CBL.Database/                # Microsoft.Build.Sql
    ├── Tables/ (Users, Loans, LoanDecisionAudit, SystemParameters)
    └── PostDeployment/Seed.sql
.github/workflows/
├── ci.yml                       # build .sqlproj → .dacpac; SQL Server como servicio; SqlPackage; pruebas
└── release.yml                  # tag v*.*.* → GitHub Release con .dacpac y zip de la API
```

**Decisión de estructura**: servicio web con cuatro proyectos de producción más dos de pruebas y el proyecto de base de datos, tal como exige la constitución (principios I y II). Las referencias solo apuntan hacia adentro: `Domain` ← `Application` ← `Infrastructure`; `Api` → `Application` + `Infrastructure`.

## Seguimiento de complejidad

Sin violaciones de la constitución; no aplica.
