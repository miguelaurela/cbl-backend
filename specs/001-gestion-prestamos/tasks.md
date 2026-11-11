---

description: "Lista de tareas de implementación: CBL Project – Gestión de préstamos bancarios (backend)"
---

# Tareas: CBL Project – Gestión de préstamos bancarios (backend)

**Entrada**: documentos de diseño en `specs/001-gestion-prestamos/`

**Requisitos previos**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/openapi.yaml](contracts/openapi.yaml), [quickstart.md](quickstart.md)

**Pruebas**: SE INCLUYEN. La constitución (principio VI) exige pruebas unitarias de dominio, validadores y casos de uso, y pruebas de integración solo en CI. Dentro de cada historia, las pruebas se escriben primero y deben fallar antes de implementar.

**Organización**: las tareas se agrupan por historia de usuario del spec para poder implementarlas y probarlas de forma independiente.

## Formato: `[ID] [P?] [Historia] Descripción`

- **[P]**: se puede ejecutar en paralelo (archivos distintos y sin dependencias pendientes)
- **[USn]**: historia de usuario a la que pertenece (trazabilidad con el spec)
- Cada tarea incluye la ruta exacta del archivo
- Las restricciones del modelo de datos se citan literalmente para no dejarlas al criterio de la implementación

## Convenciones de rutas

Solución en la raíz del repositorio, según el plan: `src/CBL.Domain`, `src/CBL.Application`, `src/CBL.Infrastructure`, `src/CBL.Api`, `tests/CBL.UnitTests`, `tests/CBL.IntegrationTests`, `db/CBL.Database`, `.github/workflows/`. Código e identificadores en inglés; documentación en español.

**Comandos de referencia**: pruebas locales `dotnet test --filter "Category!=Integration"`; las de integración se marcan con `[Trait("Category","Integration")]` y el atributo propio `[SqlServerFact]` las omite si no existe `CBL_TEST_SQL`.

---

## Fase 1: Preparación (infraestructura compartida)

**Propósito**: inicializar la solución y los proyectos.

- [x] T001 Verificar el SDK de .NET 8 (`dotnet --list-sdks`; en esta máquina solo hay 6 y 9, instalar con `winget install Microsoft.DotNet.SDK.8`) y crear `global.json` en la raíz fijando `8.0.x`
- [x] T002 Crear `CBL.sln`, `Directory.Build.props` (nullable habilitado, implicit usings, advertencias como error, C# 12) y `Directory.Packages.props` (gestión central con versiones fijadas) en la raíz (dep: T001)
- [x] T003 Crear los proyectos `src/CBL.Domain`, `src/CBL.Application`, `src/CBL.Infrastructure` y `src/CBL.Api` con referencias solo hacia adentro: `CBL.Domain` sin referencias ni paquetes; `CBL.Application` → `CBL.Domain`; `CBL.Infrastructure` → `CBL.Application`; `CBL.Api` → `CBL.Application` + `CBL.Infrastructure` (dep: T002)
- [x] T004 [P] Crear `tests/CBL.UnitTests` (→ Domain, Application, Infrastructure) y `tests/CBL.IntegrationTests` (→ Api) con xUnit, NSubstitute, FluentAssertions 6.x y `Microsoft.AspNetCore.Mvc.Testing` (dep: T002)
- [x] T005 [P] Crear `db/CBL.Database/CBL.Database.sqlproj` con el SDK `Microsoft.Build.Sql`, vacío; `dotnet build db/CBL.Database` debe producir el `.dacpac` (dep: T002)
- [x] T006 Añadir paquetes a `Directory.Packages.props` y a cada `.csproj`: Application → FluentValidation 11; Infrastructure → EF Core 8 (`SqlServer`, `InMemory`) y `Microsoft.Extensions.Identity.Core`; Api → `Microsoft.AspNetCore.Authentication.JwtBearer` 8 y Swashbuckle.AspNetCore 6 (dep: T003, T004)
- [x] T007 [P] Verificar que `.gitignore` cubre `appsettings.Development.json`, `.env` y `secrets.json`, y crear `src/CBL.Api/appsettings.json` sin secretos con las secciones `Jwt` (`Issuer`, `Audience`, `LifetimeMinutes` = 10), `Auth` (`MaxFailedAttempts` = 5, `LockoutMinutes` = 15), `Cors:AllowedOrigins`, `Database:Provider` y límites de tasa (dep: T003)

---

## Fase 2: Fundamentos (prerrequisitos bloqueantes)

**Propósito**: infraestructura básica que DEBE estar lista antes de cualquier historia de usuario.

**⚠️ CRÍTICO**: ninguna historia puede empezar hasta completar esta fase.

### Dominio

- [x] T008 [P] Crear `src/CBL.Domain/Users/Role.cs` (`Admin`, `Borrower`) y `src/CBL.Domain/Loans/LoanStatus.cs` (`Pending`, `Approved`, `Rejected`) (dep: T003)
- [x] T009 [P] Crear las excepciones en `src/CBL.Domain/Exceptions/`: `DomainException` (base), `InvalidLoanTransitionException`, `LoanNotModifiableException` y `DomainValidationException` (dep: T003)
- [x] T010 [P] Crear `src/CBL.Domain/Users/User.cs`: `Id` (Guid), `Email` (`nvarchar(256)`, obligatorio, único, normalizado en minúsculas), `FullName` (`nvarchar(200)`, obligatorio), `PasswordHash` (`nvarchar(500)`, obligatorio), `Role` (`CHECK (Role IN ('Admin','Borrower'))`, un único rol), `IsActive` (por defecto `1`) y `CreatedAt` (`datetime2(0)`, UTC); métodos de creación, actualización y desactivación lógica (dep: T008)
- [x] T011 Crear `src/CBL.Domain/Loans/Loan.cs` con los campos del modelo: `Id`, `BorrowerId`, `Amount` (`decimal(18,2)`, `CHECK (Amount > 0)`), `TermMonths` (`CHECK (TermMonths BETWEEN 1 AND 60)`), `Status` (`CHECK (Status IN ('Pending','Approved','Rejected'))`, por defecto `Pending`), `CreatedAt`, `DecidedByUserId` (NULL), `DecidedAt` (NULL) y `Version` (`NOT NULL DEFAULT 1`, lo incrementa la propia entidad en cada cambio); fábrica `Loan.Request(...)` que valida monto > 0 con 2 decimales como máximo y plazo entero 1–60; métodos `Approve(adminId, now)`, `Reject(adminId, now)` (solo desde `Pending`, si no lanzan `InvalidLoanTransitionException`), `UpdateTerms(...)` y `EnsureCanDelete()` (solo en `Pending`, si no lanzan `LoanNotModifiableException`) (dep: T008, T009)
- [x] T012 [P] Crear `src/CBL.Domain/Loans/LoanDecisionAudit.cs` de solo inserción, sin métodos de actualización: `Id`, `LoanId`, `AdminUserId`, `Decision` (`CHECK (Decision IN ('Approved','Rejected'))`), `PreviousStatus` (siempre `Pending`), `DecidedAt` (UTC) y `CorrelationId` (`nvarchar(64)`, NULL) (dep: T008)
- [x] T013 [P] Crear `src/CBL.Domain/Parameters/SystemParameter.cs`: `Key` (`nvarchar(100)`, clave primaria), `Value` (`nvarchar(500)`), `UpdatedAt`, `UpdatedByUserId` (NULL) y constantes `LoanAmountMin` y `LoanAmountMax` (dep: T003)

### Aplicación (puertos y base)

- [x] T014 [P] Crear los puertos en `src/CBL.Application/Abstractions/`: `IClock`, `ICurrentUser` (`Id`, `Role`, siempre desde el token), `IPasswordHasher`, `ITokenService`, `IReadCache`, `ILoginAttemptTracker` y `ITokenRevocationStore` (dep: T008)
- [x] T015 [P] Crear en `src/CBL.Application/Abstractions/` los puertos de persistencia `IUserRepository`, `ILoanRepository`, `ISystemParameterRepository`, `ILoanAuditRepository` e `IUnitOfWork.SaveChangesAsync`, sin tipos de EF (dep: T010, T011, T012, T013)
- [x] T016 [P] Crear las excepciones de aplicación en `src/CBL.Application/Common/Exceptions/`: `NotFoundException`, `ForbiddenException`, `UnauthorizedException`, `ValidationFailedException` (errores por campo en camelCase), `ConflictException`, `ConcurrencyConflictException` y `AccountLockedException` (con `RetryAfter`) (dep: T003)
- [x] T017 Crear `src/CBL.Application/DependencyInjection.cs` con `AddApplication()` que registra validadores y manejadores (cada historia añade los suyos) (dep: T014, T016)

### Esquema SQL (`db/CBL.Database`, fuente de verdad)

- [x] T018 [P] Crear `db/CBL.Database/Tables/Users.sql`: clave primaria `Id`, `UNIQUE(Email)`, `Email nvarchar(256)`, `FullName nvarchar(200)`, `PasswordHash nvarchar(500)`, `Role nvarchar(20)` con `CHECK (Role IN ('Admin','Borrower'))`, `IsActive bit` por defecto `1`, `CreatedAt datetime2(0)` (dep: T005, T010)
- [x] T019 [P] Crear `db/CBL.Database/Tables/Loans.sql`: claves foráneas `BorrowerId` y `DecidedByUserId` a `Users`; `Amount decimal(18,2)` con `CHECK (Amount > 0)`; `CHECK (TermMonths BETWEEN 1 AND 60)`; `Status nvarchar(20)` con `CHECK (Status IN ('Pending','Approved','Rejected'))`; `CreatedAt datetime2(0)` y `DecidedAt datetime2(0)` (NULL); `Version INT NOT NULL DEFAULT 1`; índices `(BorrowerId, Status)` y `(Status, CreatedAt)` (dep: T005, T011)
- [x] T020 [P] Crear `db/CBL.Database/Tables/LoanDecisionAudit.sql`: claves foráneas a `Loans` y a `Users`, `Decision nvarchar(20)` con `CHECK (Decision IN ('Approved','Rejected'))`, `PreviousStatus nvarchar(20)`, `DecidedAt datetime2(0)`, `CorrelationId nvarchar(64)` NULL e índice sobre `LoanId` (dep: T005, T012)
- [x] T021 [P] Crear `db/CBL.Database/Tables/SystemParameters.sql`: `[Key] nvarchar(100)` clave primaria, `Value nvarchar(500)`, `UpdatedAt datetime2(0)`, `UpdatedByUserId` NULL con clave foránea a `Users` (dep: T005, T013)
- [x] T022 Generar una sola vez el hash PBKDF2 de `123` con `PasswordHasher<User>` usando una consola desechable que NO se commitea, y anotar el hash resultante para pegarlo en `db/CBL.Database/PostDeployment/Seed.sql` (la contraseña es solo de ejemplo) (dep: T006, T010)
- [x] T023 Crear `db/CBL.Database/PostDeployment/Seed.sql` idempotente con `MERGE` (`usuario@test.com` rol `Borrower` y `admin@test.com` rol `Admin`, ambos con el hash de T022; `LoanAmountMin = 1000` y `LoanAmountMax = 100000`) y `db/CBL.Database/PostDeployment/Script.PostDeployment.sql` que lo incluye con `:r .\Seed.sql`; desplegar dos veces debe dejar 2 usuarios y 2 parámetros (dep: T018, T021, T022)

### Persistencia con EF Core

- [x] T024 Crear `src/CBL.Infrastructure/Persistence/CblDbContext.cs` con los `DbSet` de las cuatro entidades y tablas `Users`, `Loans`, `LoanDecisionAudit` y `SystemParameters`, sin migraciones (dep: T015)
- [x] T025 [P] Crear `src/CBL.Infrastructure/Persistence/Configurations/UserConfiguration.cs` mapeando de forma explícita tipos, longitudes, el índice único de `Email` y `Role` como cadena, idéntico al SQL de `Users.sql` (dep: T024, T018)
- [x] T026 [P] Crear `src/CBL.Infrastructure/Persistence/Configurations/LoanConfiguration.cs`: `decimal(18,2)`, enumeración como cadena, claves foráneas con borrado `Restrict`, índices `(BorrowerId, Status)` y `(Status, CreatedAt)`, y `Version` con `IsConcurrencyToken()` (no `rowversion`) (dep: T024, T019)
- [x] T027 [P] Crear `src/CBL.Infrastructure/Persistence/Configurations/LoanDecisionAuditConfiguration.cs` y `SystemParameterConfiguration.cs` idénticos a `LoanDecisionAudit.sql` y `SystemParameters.sql` (dep: T024, T020, T021)
- [x] T028 [P] Implementar `UserRepository`, `LoanRepository`, `SystemParameterRepository` y `LoanAuditRepository` en `src/CBL.Infrastructure/Persistence/Repositories/` (dep: T024, T015)
- [x] T029 Implementar `src/CBL.Infrastructure/Persistence/UnitOfWork.cs`: un único `SaveChangesAsync` por operación; `DbUpdateConcurrencyException` se convierte en `ConcurrencyConflictException` (dep: T024, T016)
- [x] T030 Crear `src/CBL.Infrastructure/DependencyInjection.cs` con `AddInfrastructure(config)` y el selector `Database:Provider = InMemory | SqlServer` (InMemory por defecto en Development) (dep: T028, T029)
- [x] T031 Crear `src/CBL.Infrastructure/Persistence/InMemorySeeder.cs` que inserta los mismos 2 usuarios y 2 parámetros del seed SQL, solo en Development con InMemory (dep: T022, T030)
- [x] T032 [P] Implementar `src/CBL.Infrastructure/Caching/MemoryReadCache.cs` (`IMemoryCache` con límite de tamaño y expiración **absoluta** de 5 minutos) y `src/CBL.Infrastructure/SystemClock.cs` (dep: T014)

### API: pipeline, errores y autenticación base

- [x] T033 Crear `src/CBL.Api/Middleware/ApiExceptionHandler.cs` (`IExceptionHandler` + `ProblemDetails`): `ValidationFailedException` → 422 con `errors` por campo en camelCase; JSON mal formado → 400; `UnauthorizedException` → 401; `ForbiddenException` → 403; `NotFoundException` → 404; `ConflictException`, `InvalidLoanTransitionException`, `LoanNotModifiableException` y `ConcurrencyConflictException` → 409; `AccountLockedException` → 423 con `Retry-After`; cualquier otro → 500 con texto genérico, sin detalles internos fuera de Development; siempre `application/problem+json` con `type`, `title`, `status` y `traceId` (dep: T009, T016)
- [x] T034 [P] Crear `src/CBL.Api/Middleware/SecurityHeadersMiddleware.cs`: `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, `Content-Security-Policy: default-src 'none'; frame-ancestors 'none'` (relajada solo para Swagger en Development) y `Cache-Control: no-store`, también en respuestas de error (dep: T003)
- [x] T035 [P] Crear `src/CBL.Api/Middleware/CorrelationIdMiddleware.cs`: `X-Correlation-Id` de entrada y salida, en el ámbito de logs y como `traceId`; el logging nunca registra cuerpos, contraseñas ni cabecera `Authorization` (dep: T003)
- [x] T036 [P] Crear `src/CBL.Api/Extensions/HttpPipelineExtensions.cs`: CORS con política nombrada desde `Cors:AllowedOrigins` (sin `AllowAnyOrigin`, métodos y cabeceras explícitos, incluida `Authorization`, sin credenciales), `AddRateLimiter` (global por IP, 100 solicitudes/min por defecto, y política `login` más estricta, 20/min por IP por defecto, ambos configurables en `RateLimiting:*`; el límite de `login` debe quedar por encima de `Auth:MaxFailedAttempts` para que el bloqueo sea alcanzable; 429 con `Retry-After` y `ProblemDetails`) y límite de cuerpo de 64 KB (dep: T007)
- [x] T037 Crear `src/CBL.Api/Extensions/AuthExtensions.cs` y `src/CBL.Api/Security/CurrentUser.cs` (en `CBL.Api` y no en Infrastructure porque depende de `HttpContext`): `JwtOptions` (en `src/CBL.Infrastructure/Security/JwtOptions.cs`) (`Issuer`, `Audience`, `Key` desde `Jwt__Key`, `LifetimeMinutes` = 10) con arranque fallido si falta una clave de al menos 32 bytes fuera de Development; `AddJwtBearer` validando emisor, audiencia, firma y expiración con `ClockSkew` de 30 s; policies `AdminOnly`, `BorrowerOnly` y política de respaldo "usuario autenticado"; `ICurrentUser` desde los claims (dep: T006, T014)
- [x] T038 Crear `src/CBL.Api/Extensions/ValidationExtensions.cs`: ejecutar los validadores de FluentValidation antes del caso de uso (filtro de endpoint o de acción) y lanzar `ValidationFailedException`; configurar JSON en camelCase y enumeraciones como cadena (dep: T016, T006)
- [x] T039 Crear `src/CBL.Api/Program.cs` con el pipeline en este orden exacto: manejo global de excepciones → HTTPS/HSTS (fuera de Development) → cabeceras de seguridad → CORS → rate limiting → correlation ID y logging → autenticación → autorización → endpoints; Swagger y Swagger UI solo en Development; `public partial class Program` para las pruebas (dep: T017, T030, T033, T034, T035, T036, T037, T038)

### Soporte de pruebas

- [x] T040 [P] Crear en `tests/CBL.UnitTests/Support/` `FakeClock`, una fábrica de `CblDbContext` InMemory y constructores de entidades de prueba (dep: T024, T032)
- [x] T041 [P] Crear en `tests/CBL.IntegrationTests/Support/` el atributo `SqlServerFactAttribute` (omite la prueba si no existe `CBL_TEST_SQL`), `CblWebApplicationFactory` apuntando a ese SQL Server, el restablecimiento de datos entre pruebas (borrar filas y reaplicar el seed) y un ayudante que inicia sesión con los usuarios semilla (requiere T051 y T054 de US1); todas las clases con `[Trait("Category","Integration")]` (dep: T004, T039)
- [x] T042 [P] Crear `tests/CBL.UnitTests/Api/ExceptionMappingTests.cs` que verifica la tabla excepción → código HTTP de T033 (dep: T033, T040)
- [x] T043 [P] Crear `tests/CBL.UnitTests/Architecture/ProjectReferencesTests.cs`: `CBL.Domain` sin referencias de proyecto ni paquetes y ninguna referencia hacia capas superiores (dep: T003, T040)

**Punto de control**: fundamentos listos; las historias de usuario pueden comenzar.

---

## Fase 3: Historia de usuario 1 – Iniciar sesión (Prioridad: P1) 🎯 MVP

**Objetivo**: acceso con correo y contraseña, sesión de 10 minutos y bloqueo por intentos fallidos (FR-001 a FR-003, FR-005, FR-025).

**Prueba independiente**: iniciar sesión con las credenciales semilla de cada rol y comprobar el token, la expiración y el bloqueo tras 5 fallos.

### Pruebas de la historia 1 ⚠️ (escribir primero y comprobar que fallan)

- [x] T044 [P] [US1] Pruebas de `LoginHandler` en `tests/CBL.UnitTests/Auth/LoginHandlerTests.cs`: correo inexistente, contraseña incorrecta y usuario desactivado devuelven el mismo 401 genérico; éxito devuelve `accessToken`, `tokenType` `Bearer`, `expiresAt` y `user`; el 6.º intento con el mismo correo (exista o no) lanza `AccountLockedException`; un correo con otras mayúsculas inicia sesión como el mismo usuario
- [x] T045 [P] [US1] Pruebas de `LoginAttemptTracker` en `tests/CBL.UnitTests/Auth/LoginAttemptTrackerTests.cs`: 5 fallos en la ventana de 15 minutos bloquean, el correo se normaliza (mayúsculas) y el bloqueo expira, con valores configurables
- [x] T046 [P] [US1] Pruebas de `JwtTokenService` en `tests/CBL.UnitTests/Auth/JwtTokenServiceTests.cs`: claims `sub`, `email`, `role` y `jti`, HS256, expiración = ahora + 10 minutos y rechazo de una clave de menos de 32 bytes
- [x] T047 [P] [US1] Prueba del seed en `tests/CBL.UnitTests/Auth/SeedPasswordHashTests.cs`: el hash de `Seed.sql` verifica la contraseña `123` con `PasswordHasher<User>`
- [x] T048 [P] [US1] Pruebas de integración en `tests/CBL.IntegrationTests/AuthenticationTests.cs`: sin token → 401; token vencido → 401; login correcto; credenciales incorrectas → 401 genérico; con el límite de `login` por defecto, 5 fallos con el mismo correo (exista o no) y un 6.º intento → 423 con `Retry-After`; en una prueba aparte, con el límite de `login` bajado a 3/min por la configuración de la fábrica de pruebas, el 4.º intento → 429; todos los errores en `application/problem+json`

### Implementación de la historia 1

- [x] T049 [P] [US1] Crear `LoginRequest`, `LoginResponse` y su validador en `src/CBL.Application/Auth/Login/` (correo `maxLength 256`, contraseña `maxLength 200`)
- [x] T050 [P] [US1] Implementar `src/CBL.Infrastructure/Security/PasswordHasherAdapter.cs` sobre `PasswordHasher<User>` (PBKDF2) con comparación segura
- [x] T051 [P] [US1] Implementar `src/CBL.Infrastructure/Security/JwtTokenService.cs` (HS256, 10 minutos, claims `sub`, `email`, `role`, `jti`) (dep: T037)
- [x] T052 [P] [US1] Implementar `src/CBL.Infrastructure/Security/LoginAttemptTracker.cs` en memoria sobre `IMemoryCache` por correo normalizado, con `Auth:MaxFailedAttempts` y `Auth:LockoutMinutes` validados al arrancar; las entradas caducan al terminar la ventana de 15 minutos y el almacén tiene límite de tamaño, para que correos arbitrarios no hagan crecer la memoria sin control (dep: T014)
- [x] T053 [US1] Implementar `src/CBL.Application/Auth/Login/LoginHandler.cs`: buscar al usuario por correo normalizado (minúsculas); mismo 401 genérico para correo inexistente, contraseña incorrecta y usuario desactivado; contar el fallo también para correos inexistentes; lanzar `AccountLockedException` al bloquear (dep: T049, T050, T051, T052)
- [x] T054 [US1] Crear `src/CBL.Api/Endpoints/AuthEndpoints.cs` con `POST /api/auth/login` (`[AllowAnonymous]`, política de tasa `login`, respuestas 200/400/401/422/423/429 según el contrato) (dep: T053, T039)
- [x] T055 [US1] Registrar los servicios de la historia en `src/CBL.Application/DependencyInjection.cs` y `src/CBL.Infrastructure/DependencyInjection.cs` y comprobar T044–T048 en verde (dep: T054)

**Punto de control**: la historia 1 es funcional y probable por sí sola.

---

## Fase 4: Historia de usuario 2 – Solicitar un préstamo (Prioridad: P1) 🎯 MVP

**Objetivo**: un Borrower solicita un préstamo con monto y plazo válidos (FR-006 a FR-010).

**Prueba independiente**: con sesión de prestamista, una solicitud válida queda `Pending`; una inválida devuelve el mensaje por campo; un Admin recibe 403.

### Pruebas de la historia 2 ⚠️

- [x] T056 [P] [US2] Pruebas de dominio en `tests/CBL.UnitTests/Domain/LoanRequestTests.cs`: plazo 0 y 61 rechazados, 1 y 60 aceptados; monto ≤ 0 y con más de 2 decimales rechazados; una solicitud válida nace `Pending` con `Version` 1
- [x] T057 [P] [US2] Pruebas de `RequestLoanHandler` en `tests/CBL.UnitTests/Loans/RequestLoanHandlerTests.cs`: monto igual al mínimo y al máximo aceptado; por debajo del mínimo o por encima del máximo rechazado con error en `amount`; plazo inválido con error en `termMonths`; el prestamista sale de `ICurrentUser`; los límites se leen de `SystemParameters` mediante `IReadCache`
- [x] T058 [P] [US2] Pruebas de validadores en `tests/CBL.UnitTests/Loans/CreateLoanRequestValidatorTests.cs` (monto > 0 con 2 decimales como máximo; plazo entero 1–60)
- [x] T059 [P] [US2] Pruebas de integración en `tests/CBL.IntegrationTests/LoanRequestTests.cs`: 201 con `Location` y estado `Pending`; 422 con `errors.amount` o `errors.termMonths`; Admin → 403; sin token → 401

### Implementación de la historia 2

- [x] T060 [P] [US2] Crear en `src/CBL.Application/Loans/` `LoanDto` (campos del contrato: `id`, `borrowerId`, `borrowerName`, `borrowerEmail`, `amount`, `termMonths`, `status`, `createdAt`, `decidedAt`, `decidedById`, `decidedByName`, `version`), `CreateLoanRequest` y su validador; nunca se expone una entidad
- [x] T061 [P] [US2] Crear `src/CBL.Application/Parameters/LoanLimitsReader.cs` que lee `LoanAmountMin` y `LoanAmountMax` de `ISystemParameterRepository` a través de `IReadCache` con la clave `loan-limits` (expiración absoluta de 5 minutos)
- [x] T062 [US2] Implementar `src/CBL.Application/Loans/RequestLoan/RequestLoanHandler.cs`: tomar el prestamista de `ICurrentUser`, validar contra los límites vigentes y guardar el préstamo `Pending` (dep: T060, T061, T011)
- [x] T063 [US2] Crear `src/CBL.Api/Endpoints/LoanEndpoints.cs` con `POST /api/loans` (`BorrowerOnly`, 201 con `Location`) (dep: T062, T039)
- [x] T064 [US2] Registrar los servicios de la historia en `src/CBL.Application/DependencyInjection.cs` y comprobar T056–T059 en verde (dep: T063)

**Punto de control**: las historias 1 y 2 funcionan de forma independiente.

---

## Fase 5: Historia de usuario 3 – Consultar mis préstamos y su estado (Prioridad: P1) 🎯 MVP

**Objetivo**: el prestamista ve solo sus préstamos con el estado actual y puede editar o eliminar los suyos en `Pending` (FR-011, FR-012, FR-018, FR-019).

**Prueba independiente**: con dos prestamistas, cada uno ve solo los suyos; un préstamo decidido no se puede editar ni eliminar.

### Pruebas de la historia 3 ⚠️

- [x] T065 [P] [US3] Pruebas de consultas en `tests/CBL.UnitTests/Loans/LoanQueriesTests.cs`: `GetMyLoans` devuelve solo los propios; `GetLoanById` devuelve `NotFoundException` para un préstamo ajeno; `GetLoanStatus` con acierto de caché no llama al repositorio, la autorización se evalúa antes de devolver el dato cacheado y el prestamista B no puede leer el estado cacheado del A
- [x] T066 [P] [US3] Pruebas de `UpdateLoanHandler` y `DeleteLoanHandler` en `tests/CBL.UnitTests/Loans/UpdateDeleteLoanHandlerTests.cs`: el dueño edita o elimina solo en `Pending`; un préstamo decidido lanza conflicto; un préstamo ajeno devuelve `NotFoundException`; al editar se revalidan monto y plazo con los límites vigentes; un Admin elimina solo `Pending`
- [x] T067 [P] [US3] Pruebas de integración en `tests/CBL.IntegrationTests/MyLoansTests.cs`: un prestamista ve solo sus préstamos; el préstamo ajeno devuelve 404; editar y eliminar en `Pending` funciona y en un préstamo decidido da 409; `GET /api/loans/{id}/status` refleja el cambio

### Implementación de la historia 3

- [x] T068 [P] [US3] Implementar `GetMyLoansHandler` y `GetLoanByIdHandler` en `src/CBL.Application/Loans/Queries/` (dueño o Admin; si no, `NotFoundException` para evitar enumeración)
- [x] T069 [P] [US3] Implementar `src/CBL.Application/Loans/Queries/GetLoanStatusHandler.cs` con lectura a través de `IReadCache`: clave `loan-status:{id}`, expiración absoluta de 5 minutos, la caché guarda estado y dueño y la autorización se evalúa siempre antes de responder
- [x] T070 [P] [US3] Implementar `UpdateLoanHandler` y `DeleteLoanHandler` (con validadores) en `src/CBL.Application/Loans/Commands/`: el Borrower dueño edita o elimina solo en `Pending`; un Admin elimina solo `Pending`; al editar se revalidan los límites vigentes
- [x] T071 [US3] Añadir a `src/CBL.Api/Endpoints/LoanEndpoints.cs` `GET /api/loans/mine` (`BorrowerOnly`), `GET /api/loans/{id}`, `GET /api/loans/{id}/status`, `PUT /api/loans/{id}` (`BorrowerOnly`) y `DELETE /api/loans/{id}` (dueño o Admin) con las respuestas del contrato (dep: T068, T069, T070)
- [x] T072 [US3] Registrar los servicios de la historia en `src/CBL.Application/DependencyInjection.cs` y comprobar T065–T067 en verde (dep: T071)

**Punto de control**: las historias 1 a 3 son funcionales.

---

## Fase 6: Historia de usuario 4 – Ver todas las solicitudes (Prioridad: P1) 🎯 MVP

**Objetivo**: el Admin ve todas las solicitudes con prestamista, monto y estado, con filtro por estado y sin paginación (FR-013).

**Prueba independiente**: con préstamos de varios prestamistas, el Admin los ve todos y un Borrower recibe 403.

### Pruebas de la historia 4 ⚠️

- [x] T073 [P] [US4] Pruebas de `GetAllLoansHandler` en `tests/CBL.UnitTests/Loans/GetAllLoansHandlerTests.cs`: sin filtro devuelve todos; con `status` devuelve solo ese estado; orden del más reciente al más antiguo; incluye nombre y correo del prestamista; sin paginación
- [x] T074 [P] [US4] Pruebas de integración en `tests/CBL.IntegrationTests/AdminLoansListTests.cs`: el Admin ve los préstamos de todos; el filtro `?status=Pending` funciona; un Borrower recibe 403; sin token 401

### Implementación de la historia 4

- [x] T075 [US4] Implementar `src/CBL.Application/Loans/Queries/GetAllLoansHandler.cs` y el método de listado con filtro opcional por estado en `LoanRepository` (más recientes primero, apoyado en el índice `(Status, CreatedAt)`), sin paginación (dep: T028)
- [x] T076 [US4] Añadir `GET /api/loans` (`AdminOnly`, parámetro opcional `status`) a `src/CBL.Api/Endpoints/LoanEndpoints.cs`, registrar el servicio y comprobar T073 y T074 en verde (dep: T075)

**Punto de control**: las historias 1 a 4 funcionan.

---

## Fase 7: Historia de usuario 5 – Aprobar o rechazar una solicitud (Prioridad: P1) 🎯 MVP

**Objetivo**: el Admin decide sobre préstamos `Pending`; la decisión es final, queda auditada y ante decisiones simultáneas solo una prevalece (FR-014 a FR-019).

**Prueba independiente**: decidir sobre un préstamo `Pending` y comprobar estado, auditoría (quién y cuándo), 409 en la segunda decisión y estado fresco tras decidir.

### Pruebas de la historia 5 ⚠️

- [x] T077 [P] [US5] Pruebas de dominio en `tests/CBL.UnitTests/Domain/LoanDecisionTests.cs`: `Approve` y `Reject` desde `Pending` fijan `DecidedByUserId` y `DecidedAt` e incrementan `Version`; una segunda decisión lanza `InvalidLoanTransitionException`; `UpdateTerms` y `EnsureCanDelete` tras decidir lanzan `LoanNotModifiableException`
- [x] T078 [P] [US5] Pruebas de `ApproveLoanHandler` y `RejectLoanHandler` en `tests/CBL.UnitTests/Loans/DecideLoanHandlerTests.cs`: se añade una fila de auditoría con el id del admin y la fecha; un único `SaveChangesAsync`; la segunda decisión da conflicto; `loan-status:{id}` se elimina solo después de guardar y no se elimina si el guardado falla; un `version` opcional que no coincide da conflicto; `DbUpdateConcurrencyException` se traduce a `ConcurrencyConflictException`
- [x] T079 [P] [US5] Prueba de concurrencia con EF InMemory en `tests/CBL.UnitTests/Persistence/LoanConcurrencyTests.cs`: dos contextos sobre el mismo almacén y el mismo préstamo; el segundo `SaveChangesAsync` lanza `DbUpdateConcurrencyException`, lo que demuestra que el token `Version` funciona
- [x] T080 [P] [US5] Pruebas de integración en `tests/CBL.IntegrationTests/DecisionTests.cs`: Borrower aprobando → 403; aprobar cambia el estado y deja una fila en `LoanDecisionAudit`; decidir dos veces → 409 sin cambios; el estado consultado después de decidir es el nuevo; un Admin elimina un préstamo `Pending` y uno decidido da 409
- [x] T081 [P] [US5] Prueba de integración de carrera en `tests/CBL.IntegrationTests/ConcurrencyRaceTests.cs`: dos decisiones simultáneas con `Task.WhenAll`; exactamente una responde 200 y la otra 409, y existe una sola fila de auditoría

### Implementación de la historia 5

- [x] T082 [US5] Implementar `ApproveLoanHandler` y `RejectLoanHandler` (con `DecideLoanRequest` de `version` opcional) en `src/CBL.Application/Loans/Commands/`: cargar el préstamo, llamar al método de la entidad, añadir la auditoría con el `CorrelationId` y guardar todo en **un único** `SaveChangesAsync`; invalidar `loan-status:{id}` solo tras el guardado correcto (dep: T011, T012, T029, T032)
- [x] T083 [US5] Añadir `POST /api/loans/{id}/approve` y `POST /api/loans/{id}/reject` (`AdminOnly`, cuerpo opcional) a `src/CBL.Api/Endpoints/LoanEndpoints.cs` con 200/401/403/404/409/429 según el contrato (dep: T082)
- [x] T084 [US5] Registrar los servicios de la historia en `src/CBL.Application/DependencyInjection.cs` y comprobar T077–T081 en verde (dep: T083)

**Punto de control — CORTE DE MVP**: con las historias 1 a 5 (más las tareas T102, T104 y T108 de la última fase) está el flujo central completo: login → solicitar → listar → aprobar o rechazar → estado actualizado.

---

## Fase 8: Historia de usuario 6 – Gestionar usuarios (Prioridad: P2)

**Objetivo**: el Admin crea, consulta, actualiza y desactiva usuarios (FR-020 a FR-022).

**Prueba independiente**: crear un usuario con contraseña válida, consultarlo, actualizarlo y desactivarlo; el desactivado no puede iniciar sesión y su historial se conserva.

### Pruebas de la historia 6 ⚠️

- [x] T085 [P] [US6] Pruebas de manejadores en `tests/CBL.UnitTests/Users/UserHandlersTests.cs`: crear (correo único y normalizado, contraseña de al menos 8 caracteres guardada con hash), actualizar (`fullName`, `role`, `isActive` y contraseña opcional), un admin que intenta desactivarse o cambiarse el rol a sí mismo lanza `ForbiddenException`, reactivar a otro usuario funciona, usuario inexistente → `NotFoundException`
- [x] T086 [P] [US6] Pruebas de validadores en `tests/CBL.UnitTests/Users/UserValidatorsTests.cs`: contraseña de 7 caracteres rechazada y de 8 aceptada; correo con formato inválido; rol fuera de `Admin`/`Borrower`
- [x] T087 [P] [US6] Pruebas de integración en `tests/CBL.IntegrationTests/UsersTests.cs`: CRUD completo; contraseña de 7 caracteres → 422; correo duplicado (incluso con otras mayúsculas) → 409; Borrower → 403; admin que se desactiva o se cambia el rol → 403; reactivar a otro usuario funciona; usuario desactivado → 401 en el login y sus préstamos se conservan

### Implementación de la historia 6

- [x] T088 [P] [US6] Crear en `src/CBL.Application/Users/` los DTOs `UserDto`, `CreateUserRequest` y `UpdateUserRequest` y sus validadores (correo `maxLength 256`, `fullName maxLength 200`, contraseña `minLength 8` y `maxLength 200`, `role` en `Admin`/`Borrower`)
- [x] T089 [P] [US6] Implementar `CreateUserHandler`, `GetUsersHandler`, `GetUserByIdHandler` y `UpdateUserHandler` en `src/CBL.Application/Users/` (correo único, contraseña con hash, desactivación lógica por `isActive=false`, nunca se elimina a un usuario con historial, un admin no se desactiva a sí mismo)
- [x] T090 [US6] Crear `src/CBL.Api/Endpoints/UserEndpoints.cs` con `GET /api/users`, `POST /api/users`, `GET /api/users/{id}` y `PUT /api/users/{id}` (`AdminOnly`) con las respuestas del contrato (dep: T088, T089)
- [x] T091 [US6] Registrar los servicios de la historia en `src/CBL.Application/DependencyInjection.cs` y comprobar T085–T087 en verde (dep: T090)

**Punto de control**: las historias 1 a 6 funcionan.

---

## Fase 9: Historia de usuario 7 – Gestionar los límites de monto (Prioridad: P2)

**Objetivo**: el Admin consulta y modifica los límites; el cambio aplica a solicitudes nuevas sin redesplegar (FR-023, FR-024).

**Prueba independiente**: modificar los límites y comprobar que una solicitud nueva se valida con los valores actualizados.

### Pruebas de la historia 7 ⚠️

- [x] T092 [P] [US7] Pruebas de manejadores y validador en `tests/CBL.UnitTests/Parameters/LoanLimitsHandlersTests.cs`: mínimo > 0 y máximo > mínimo; la actualización elimina la clave `loan-limits` de la caché; la siguiente solicitud usa los valores nuevos; los préstamos ya registrados conservan su monto
- [x] T093 [P] [US7] Pruebas de integración en `tests/CBL.IntegrationTests/LoanLimitsTests.cs`: cualquier usuario autenticado lee los límites; Borrower modificando → 403; el Admin cambia los límites y la siguiente solicitud se valida con ellos; valores inválidos → 422

### Implementación de la historia 7

- [x] T094 [P] [US7] Crear `LoanLimitsDto` (`minAmount`, `maxAmount`) y su validador (mínimo > 0 y máximo > mínimo) en `src/CBL.Application/Parameters/`
- [x] T095 [US7] Implementar `GetLoanLimitsHandler` y `UpdateLoanLimitsHandler` en `src/CBL.Application/Parameters/` reutilizando `LoanLimitsReader` (T061): persistir en `SystemParameters` con `UpdatedAt` y `UpdatedByUserId`, y eliminar `loan-limits` de la caché después de guardar (dep: T094, T061)
- [x] T096 [US7] Crear `src/CBL.Api/Endpoints/ParameterEndpoints.cs` con `GET /api/parameters/loan-limits` (autenticado) y `PUT /api/parameters/loan-limits` (`AdminOnly`), registrar los servicios y comprobar T092 y T093 en verde (dep: T095)

**Punto de control**: las historias 1 a 7 funcionan.

---

## Fase 10: Historia de usuario 8 – Cerrar sesión (Prioridad: P2)

**Objetivo**: al cerrar sesión el token deja de ser válido en la API aunque no hayan pasado 10 minutos (FR-004).

**Prueba independiente**: cerrar sesión y reutilizar el mismo token devuelve 401.

### Pruebas de la historia 8 ⚠️

- [x] T097 [P] [US8] Pruebas del almacén y del manejador en `tests/CBL.UnitTests/Auth/LogoutTests.cs`: revocar un `jti` lo marca como revocado; la entrada expira junto con el token (no crece sin límite); otros tokens no se ven afectados
- [x] T098 [P] [US8] Pruebas de integración en `tests/CBL.IntegrationTests/LogoutTests.cs`: `POST /api/auth/logout` responde 204 y reutilizar el mismo token responde 401; un token de otro usuario sigue válido

### Implementación de la historia 8

- [x] T099 [P] [US8] Implementar `src/CBL.Infrastructure/Security/TokenRevocationStore.cs` en memoria con expiración igual a la del token
- [x] T100 [US8] Implementar `src/CBL.Application/Auth/Logout/LogoutHandler.cs` y `POST /api/auth/logout` (autenticado, 204) en `src/CBL.Api/Endpoints/AuthEndpoints.cs` (dep: T099, T054)
- [x] T101 [US8] En `src/CBL.Api/Extensions/AuthExtensions.cs` hacer que `OnTokenValidated` de JwtBearer rechace con 401 los tokens cuyo `jti` esté revocado, registrar los servicios y comprobar T097 y T098 en verde (dep: T100, T037)

**Punto de control**: las ocho historias funcionan.

---

## Fase 11: Pulido y asuntos transversales

**Propósito**: mejoras que afectan a varias historias, CI y entrega.

- [x] T102 [P] Crear `tests/CBL.IntegrationTests/SchemaMappingTests.cs`: comparar el modelo de EF (tablas, columnas, tipos, nulabilidad, longitudes máximas, claves foráneas e índice único de `Email`) con `INFORMATION_SCHEMA` y `sys.*` del esquema desplegado desde el `.dacpac`, y comprobar que el seed deja 2 usuarios y 2 parámetros; cambiar a propósito una longitud en `OnModelCreating` debe hacerla fallar (dep: T023, T041)
- [ ] T103 [P] Crear `tests/CBL.IntegrationTests/SecurityPipelineTests.cs`: cabeceras de seguridad presentes incluso en errores, un origen no permitido no recibe cabeceras CORS, cuerpo de más de 64 KB → 413, límite de tasa global → 429 y los logs no contienen `password` ni valores de `Authorization` (dep: T041)
- [x] T104 Crear `.github/workflows/ci.yml`: compilar `db/CBL.Database` a `.dacpac`; levantar `mcr.microsoft.com/mssql/server` como contenedor de servicio con una contraseña temporal que no es un secreto del repositorio; publicar el esquema con `SqlPackage`; definir `CBL_TEST_SQL`; ejecutar las pruebas unitarias y de integración; subir el `.dacpac` como artefacto en cada ejecución y, en `main`, generar además el `DeployReport` (dep: T102)
- [ ] T105 Crear `.github/workflows/release.yml`: un tag `v*.*.*` reconstruye, ejecuta las pruebas unitarias y crea un GitHub Release con el `.dacpac`, el zip de `dotnet publish` de la API y `SHA256SUMS.txt`, usando solo `GITHUB_TOKEN` y sin despliegue en la nube (dep: T104)
- [ ] T106 [P] Completar Swagger en `src/CBL.Api/Extensions/SwaggerExtensions.cs`: esquema de seguridad Bearer, etiquetas por grupo, comentarios XML y `ProducesResponseType` con `ProblemDetails` para 200/201/204/400/401/403/404/409/422/423/429; añadir una prueba de que `/swagger` responde 404 fuera de Development (dep: T071, T083, T090, T096, T100)
- [ ] T107 [P] Exportar `openapi.json` como artefacto de CI y compararlo con `specs/001-gestion-prestamos/contracts/openapi.yaml` para detectar divergencias del contrato con el frontend (post-MVP) (dep: T104, T106)
- [x] T108 [P] Actualizar `README.md` y `README.es.md`: ejecución local con InMemory, credenciales semilla (solo de ejemplo), cómo correr las pruebas y por qué las de integración se omiten en local, variables de entorno (`Jwt__Key`, `ConnectionStrings__Cbl`, `Cors__AllowedOrigins`, `Database__Provider`), y el aviso de que caché, bloqueo de login y revocación de tokens viven en memoria (una sola instancia)
- [ ] T109 [P] Crear `docs/decisions/0026-in-memory-lockout-and-token-revocation.md` (bilingüe, inglés y luego `## Español`) que documente el bloqueo por correo y la revocación de tokens en memoria, incluido el riesgo aceptado de que cualquiera puede bloquear 15 minutos el correo de otra persona con 5 intentos fallidos, y añadir su fila a `docs/decisions/README.md`
- [ ] T110 Revisión de seguridad sobre `.gitignore`, `src/CBL.Api/appsettings.json` y `.github/workflows/`: ningún secreto en el repositorio, `git diff --cached` revisado en busca de claves pegadas en los artefactos de Spec Kit y de los agentes, `Jwt__Key` solo por entorno o user-secrets
- [ ] T111 Ejecutar la validación de `specs/001-gestion-prestamos/quickstart.md` (escenarios 1 a 20; el 12 y la coincidencia EF ↔ esquema solo en CI) y registrar los resultados (dep: todas las historias)
- [ ] T112 [P] Crear `tests/CBL.IntegrationTests/StatusQueryPerformanceTests.cs` que verifica SC-006: 100 consultas repetidas a `GET /api/loans/{id}/status` con percentil 95 menor de 1 segundo, y que el estado devuelto tras una decisión es el nuevo (dep: T041, T069, T083)
- [ ] T113 Ejecutar `dotnet test` completo en local (integración omitida) y confirmar el primer pipeline en verde de `.github/workflows/ci.yml` en GitHub Actions; abrir una PR borrador temprana por el riesgo de ADR-0021 (dep: T104, T111, T112)

---

## Dependencias y orden de ejecución

### Dependencias entre fases

- **Preparación (Fase 1)**: sin dependencias; puede empezar de inmediato.
- **Fundamentos (Fase 2)**: depende de la Fase 1 y BLOQUEA todas las historias.
- **Historias (Fases 3–10)**: todas dependen de la Fase 2; luego pueden ir en paralelo (si hay personas) o en orden de prioridad (P1 → P2).
- **Pulido (Fase 11)**: depende de las historias deseadas; T102, T104 y T108 forman parte del MVP.

### Dependencias entre historias

- **US1 (login)**: tras la Fase 2; sin dependencias de otras historias. Es **prerrequisito de las pruebas de integración de US2 a US8**: el ayudante de T041 obtiene los tokens a través del login (T051 y T054), así que el orden de MVP US1 → US5 es obligatorio.
- **US2 (solicitar)**: tras la Fase 2 y US1; su lógica de aplicación es independiente, pero sus pruebas de integración necesitan el login de US1.
- **US3 (mis préstamos)**: tras la Fase 2; para ver datos reales necesita US2 (o datos sembrados en las pruebas).
- **US4 (lista del admin)**: tras la Fase 2; no depende de US3.
- **US5 (decidir)**: tras la Fase 2; usa la entidad `Loan` y `LoanStatus` de la Fase 2; el estado fresco se verifica con `GetLoanStatus` (T069) de US3.
- **US6, US7, US8 (P2)**: tras la Fase 2; US7 reutiliza `LoanLimitsReader` (T061) de US2 y US8 amplía `AuthExtensions` (T037) y `AuthEndpoints` (T054) de US1.

### Dentro de cada historia

- Las pruebas se escriben primero y deben fallar antes de implementar.
- DTOs y puertos antes de manejadores; manejadores antes de endpoints; registrar en la inyección de dependencias al final.
- Una historia se completa (puntos de control) antes de pasar a la siguiente prioridad.

### Oportunidades de paralelismo

- En la Fase 1: T004, T005 y T007 en paralelo tras T002/T003.
- En la Fase 2: T008–T010, T012–T016 (dominio y puertos); T018–T021 (tablas SQL); T025–T027 (configuraciones de EF); T034–T036 (middleware); T040–T043 (soporte de pruebas).
- Dentro de cada historia: todas las pruebas marcadas `[P]` juntas y las implementaciones `[P]` (archivos distintos).
- Tras la Fase 2, las historias US1–US5 pueden repartirse entre personas; US6, US7 y US8 son independientes entre sí.

### Ejemplo de paralelismo: historia 5

```text
# Pruebas de la historia 5, todas juntas:
Task: "Pruebas de dominio en tests/CBL.UnitTests/Domain/LoanDecisionTests.cs"            (T077)
Task: "Pruebas de DecideLoanHandler en tests/CBL.UnitTests/Loans/DecideLoanHandlerTests.cs" (T078)
Task: "Prueba de concurrencia InMemory en tests/CBL.UnitTests/Persistence/LoanConcurrencyTests.cs" (T079)
Task: "Pruebas de integración en tests/CBL.IntegrationTests/DecisionTests.cs"            (T080)
Task: "Carrera de dos decisiones en tests/CBL.IntegrationTests/ConcurrencyRaceTests.cs"  (T081)
```

---

## Estrategia de implementación

### Primero el MVP (historias 1 a 5)

1. Completar la Fase 1 (Preparación).
2. Completar la Fase 2 (Fundamentos): crítico, bloquea todas las historias.
3. Completar US1 → US2 → US3 → US4 → US5, validando cada punto de control.
4. Añadir del Pulido solo lo necesario para el MVP: **T102** (coincidencia EF ↔ esquema), **T104** (`ci.yml`) y **T108** (README).
5. **DETENERSE Y VALIDAR**: login → solicitar → listar → aprobar o rechazar → estado actualizado; pipeline de CI en verde.

### Entrega incremental

1. Preparación + Fundamentos → base lista.
2. Historias 1 a 5 → MVP demostrable.
3. US6 (usuarios) → US7 (límites) → US8 (cierre de sesión), cada una probada de forma independiente.
4. Pulido restante: `release.yml` (T105), Swagger completo (T106), exportación de `openapi.json` (T107, posterior al MVP), ADR (T109) y validación final (T111, T112).

### Si falta tiempo (se recorta primero)

T107 → T105 → T109 → US8 → US7 → US6. Las historias P1 y el pipeline de CI no se recortan.

## Notas

- `[P]` = archivos distintos y sin dependencias pendientes.
- La etiqueta `[USn]` enlaza cada tarea con su historia para la trazabilidad.
- Cada historia debe poder completarse y probarse de forma independiente.
- Verificar que las pruebas fallan antes de implementar.
- Un commit por tarea o por grupo lógico de tareas, con el identificador de la tarea (por ejemplo `feat(domain): Loan entity (T011)`).
- Evitar tareas vagas, conflictos de archivo y dependencias entre historias que rompan su independencia.
