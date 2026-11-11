# Investigación (Fase 0): gestión de préstamos bancarios – backend

**Rama**: `001-gestion-prestamos` | **Fecha**: 2026-11-11 | **Plan**: [plan.md](plan.md)

Todas las incógnitas del contexto técnico quedan resueltas aquí. Las decisiones ya registradas como ADR se citan y no se reabren.

## R1. Plataforma y estructura de la solución

- **Decisión**: .NET 8 LTS, ASP.NET Core Web API, C# 12; solución `CBL.sln` con `src/` (4 proyectos), `tests/` (2) y `db/CBL.Database`; `global.json` fija el SDK 8.0.x y `Directory.Build.props` activa nullable, advertencias como error e implicit usings.
- **Razón**: ADR-0001/0003 y constitución (I). Las referencias solo apuntan hacia adentro, lo que se verifica con una prueba de arquitectura sencilla (referencias de proyecto) o en revisión.
- **Alternativas**: un solo proyecto con carpetas (descartada por ADR-0003); cortes verticales (descartada: menos explícito para el evaluador).
- **Nota**: en esta máquina solo hay SDK 6 y 9. Con `global.json` apuntando a 8 hace falta instalar el SDK 8 (`winget install Microsoft.DotNet.SDK.8`) antes de compilar.

## R2. Casos de uso sin MediatR

- **Decisión**: una clase por caso de uso (`RequestLoanCommand` + handler, `GetMyLoansQuery` + handler, etc.), registrada por DI con un método de extensión `AddApplication()`. Los controladores llaman al handler directamente.
- **Razón**: ADR-0004; menos piezas para una prueba de este tamaño y fácil de probar con dobles.
- **Alternativas**: MediatR (descartada por ADR-0004: dependencia y bus innecesarios).

## R3. Persistencia: `.sqlproj` + EF Core sin migraciones

- **Decisión**: `db/CBL.Database` (SDK `Microsoft.Build.Sql`) define las cuatro tablas, restricciones `CHECK`, claves foráneas, índices, la columna `Version INT NOT NULL DEFAULT 1` y un `PostDeployment/Seed.sql` idempotente (`MERGE`). `CblDbContext.OnModelCreating` declara de forma explícita nombres, tipos, longitudes, precisión, índices y token de versión para coincidir con el SQL. Sin migraciones de EF.
- **Razón**: ADR-0005 y constitución (II). Dos descripciones del esquema es el riesgo; se mitiga con la prueba de integración de coincidencia EF ↔ `.sqlproj` en CI (R14).
- **Alternativas**: migraciones de EF (descartadas por ADR-0005); procedimientos almacenados para aprobar (descartados: la lógica vive en el dominio).

## R4. Proveedor de base de datos por entorno

- **Decisión**: `Database:Provider = InMemory | SqlServer`. InMemory (con seeder en código solo para Development) para ejecución local y pruebas unitarias; SQL Server para CI y para quien defina `CBL_TEST_SQL` o configure `SqlServer`.
- **Razón**: ADR-0006/0021; el desarrollador no tiene Docker. InMemory no tiene transacciones reales ni restricciones relacionales; lo que depende del motor se prueba en CI.
- **Alternativas**: SQLite en memoria (motor distinto, no prueba SQL Server); LocalDB (solo Windows y no replicable en CI).

## R5. Concurrencia optimista

- **Decisión**: `Loan.Version` (int) como token de concurrencia (`IsConcurrencyToken()`), incrementado por la propia entidad en cada cambio de estado. SQL Server aplica `UPDATE … WHERE Id = @id AND Version = @anterior`; si afecta a 0 filas, EF lanza `DbUpdateConcurrencyException`, que la unidad de trabajo convierte en `ConcurrencyConflictException` → HTTP 409. Aprobar/rechazar guarda préstamo y auditoría en **un único** `SaveChangesAsync` (transacción implícita).
- **Razón**: ADR-0007; funciona también con InMemory (no depende de `rowversion`), lo que permite una prueba unitaria con dos contextos. FR-017 y SC-004.
- **Cuerpo opcional**: aprobar/rechazar admite `{ "version": n }`. Si llega y no coincide con la versión actual → 409. Una segunda decisión sobre un préstamo ya decidido siempre es 409 (transición inválida).
- **Alternativas**: bloqueo pesimista (descartada por ADR-0007); `rowversion` (no existe en InMemory).

## R6. Validación

- **Decisión**: FluentValidation 11 en `CBL.Application` (validadores de DTOs de entrada: monto > 0 y con 2 decimales como máximo, plazo entero 1–60, correo, contraseña ≥ 8, límites con mínimo > 0 y máximo > mínimo). Los límites de monto vigentes se comprueban en el caso de uso (dependen de datos, no solo de formato). Las invariantes también viven en el dominio (`Loan.Request` valida plazo y monto > 0).
- **Estado HTTP**: errores de validación → **422** con `errors` por campo en camelCase; JSON mal formado → **400**.
- **Razón**: FR-008–010, FR-021, SC-007; el frontend acepta 400 o 422 y espera claves camelCase.

## R7. Autenticación, sesión y bloqueo

- **Decisión**: JWT HS256, 10 minutos, sin refresh, claims `sub`, `email`, `role`, `jti`; validación de emisor, audiencia, firma y expiración con `ClockSkew` de 30 s. La clave sale de `Jwt__Key` (variable de entorno o user-secrets); en producción la app no arranca sin una clave de al menos 32 bytes. `LoginResponse` incluye `accessToken`, `tokenType`, `expiresAt` y `user`.
- **Bloqueo**: 5 intentos fallidos por **correo** en 15 minutos (ambos configurables: `Auth:MaxFailedAttempts`, `Auth:LockoutMinutes`), exista o no el correo. Se implementa con `ILoginAttemptTracker` (adaptador en memoria sobre `IMemoryCache`, claves con el correo normalizado). Respuesta de bloqueo: **423** con `Retry-After` y mensaje que no revela si el correo existe. Credenciales incorrectas y correo inexistente devuelven el mismo **401** genérico; un usuario desactivado también (FR-022).
- **Razón**: ADR-0009/0018, FR-001–003, aclaraciones 1–3. Guardar el contador en `Users` no sirve para correos inexistentes.
- **Contrapartida**: el contador se pierde al reiniciar el proceso y no se comparte entre instancias; se documenta en el README (una sola instancia).
- **Cierre de sesión (FR-004, historia 8)**: `POST /api/auth/logout` (204, requiere sesión) añade el `jti` del token a `ITokenRevocationStore`, un almacén en memoria cuya entrada expira junto con el token (máx. 10 min, por lo que no crece sin límite). El manejador `OnTokenValidated` de JwtBearer rechaza con 401 cualquier token cuyo `jti` esté revocado. Sin esto, un token "cerrado" seguiría siendo válido en la API hasta su expiración. Misma contrapartida que el bloqueo: se pierde al reiniciar el proceso y no se comparte entre instancias, aceptable para una instancia y tokens de vida corta. **Es un endpoint nuevo respecto al contrato asumido por el frontend**: el frontend debe llamarlo al cerrar sesión (si no lo hace, el cierre sigue siendo seguro en el cliente pero no en el servidor).
- **Alternativas del cierre de sesión**: solo descartar el token en el cliente (más simple, pero incumple el escenario de aceptación en la API durante hasta 10 minutos); refresh token con revocación (descartado por ADR-0009).
- **Usuario desactivado con sesión vigente**: no se consulta la base en cada petición; pierde el acceso al expirar el token (máx. 10 min), según la aclaración 2.
- **Alternativas**: refresh token en cookie `HttpOnly` (ADR-0009: evolución futura); tabla de intentos en base de datos (más piezas sin necesidad).

## R8. Autorización

- **Decisión**: policies `AdminOnly` y `BorrowerOnly`, más política de respaldo "usuario autenticado" (todo endpoint es privado salvo `POST /api/auth/login`, marcado `[AllowAnonymous]`). El id del usuario sale siempre de `ICurrentUser` (claims); ningún DTO de entrada lleva un id de propietario. Para recursos de otro usuario se responde 404 y no 403, para no permitir enumeración.
- **Razón**: constitución (III), FR-005, FR-011, SC-002.

## R9. Contraseñas

- **Decisión**: `PasswordHasher<T>` de `Microsoft.Extensions.Identity.Core` (PBKDF2). El seed incluye el hash precalculado de `123` (solo de ejemplo). La API exige ≥ 8 caracteres al crear usuarios; el seed no pasa por la API.
- **Razón**: ADR-0018/0023; FR-021, FR-025.

## R10. Pipeline de middleware

- **Decisión** (orden exacto): manejo global de excepciones (`IExceptionHandler` + `ProblemDetails`) → HTTPS/HSTS (fuera de Development) → cabeceras de seguridad (`nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, CSP restrictiva, `Cache-Control: no-store`) → CORS con origen del frontend desde `Cors:AllowedOrigins` (sin `AllowAnyOrigin`, métodos y cabeceras explícitos, incluida `Authorization`) → rate limiting (global por IP, 100 solicitudes/min por defecto, y política más estricta `login`, 20/min por IP por defecto, ambos configurables en `RateLimiting:*`; 429 con `Retry-After`; el límite de `login` debe quedar por encima de los 5 intentos del bloqueo para que este último sea alcanzable) → correlation ID (`X-Correlation-Id`, en el ámbito de logs y como `traceId`) y logging sin cuerpos, contraseñas ni tokens → autenticación → autorización → endpoints. Límite de tamaño de cuerpo (64 KB).
- **Razón**: ADR-0010 y `docs/00-analysis.md` §7; constitución (III).
- **Nota**: el límite de tasa por IP y el bloqueo por correo son mecanismos distintos; las pruebas (R14) cubren ambos.

## R11. Caché de lectura

- **Decisión**: puerto `IReadCache` implementado con `IMemoryCache` (con límite de tamaño). Claves `loan-status:{id}` y `loan-limits`; expiración **absoluta** de 5 minutos. La caché guarda estado y dueño; la autorización se evalúa siempre antes de devolver el dato. Tras `SaveChangesAsync` correcto de una decisión se elimina `loan-status:{id}`; `UpdateLoanLimits` elimina `loan-limits`. Si el guardado falla, no se invalida nada.
- **Razón**: ADR-0011/0023, FR-018/019, SC-003/006. La expiración deslizante se descarta por poder servir datos obsoletos indefinidamente.
- **Alternativas**: Redis (evolución para escalar horizontalmente); sin caché (incumple el requisito).

## R12. Contrato con el frontend

- **Decisión**: contrato REST en `contracts/openapi.yaml`, derivado de la tabla de rutas del borrador y del contrato asumido por `cbl-frontend`: prefijo `/api`, JSON camelCase, fechas ISO 8601 UTC, importes como número, enumeraciones como cadenas, ids GUID (cadena opaca). Respuestas de error siempre `application/problem+json`. El documento definitivo lo generará Swashbuckle desde el código (solo en Development) y se exportará como artefacto de CI (post-MVP); el archivo de `contracts/` es la referencia previa.
- **Conciliación con el frontend (lista de 9 puntos de `tasks-frontend.md`)**: rutas y `/api` confirmados, `mine` como ruta propia, ids GUID y enumeraciones como cadena, `version` opcional en decisiones, lockout **423** con `Retry-After`, `expiresAt` presente, validación **422** con claves camelCase, límites en `/api/parameters/loan-limits` legibles por cualquier usuario autenticado, contraseña mínima de 8 y CORS con `Authorization`.
- **Alternativas**: generar el contrato solo desde el código (no existiría antes de implementar; el frontend lo necesita en su Fase 4).

## R13. Consultas y reglas de listado

- **Decisión**: `GET /api/loans` (solo Admin) devuelve todos los préstamos con filtro opcional `status`, ordenados del más reciente al más antiguo y **sin paginación** (FR-013). `GET /api/loans/mine` devuelve solo los del Borrower. `GET /api/loans/{id}` respalda FR-011 y FR-013 (detalle del propio préstamo, o de cualquiera para el Admin). Edición (`PUT`) y borrado (`DELETE`) solo sobre préstamos Pendientes, con las reglas por rol de FR-012; al editar se revalidan monto y plazo con los límites vigentes (supuesto por defecto del spec).
- **Razón**: FR-011–013, ADR-0023/0025. Se descartó paginar porque el volumen es pequeño y el frontend no la usa.

## R14. Estrategia de pruebas

- **Unitarias (local, sin Docker)**: dominio (`Loan`, transiciones, versión), validadores, casos de uso con dobles (autorización por dueño, caché y orden de invalidación, auditoría con admin y fecha, conflicto de concurrencia), `Login` y JWT (claims, 10 min, mensaje genérico), tracker de bloqueo (5 fallos, ventana de 15 min, correo inexistente), mapeo de excepciones a estados HTTP, y una prueba con dos contextos InMemory que demuestra que el token `Version` lanza `DbUpdateConcurrencyException`. Comando: `dotnet test --filter "Category!=Integration"`.
- **Integración (solo CI)**: `WebApplicationFactory<Program>` contra el SQL Server del contenedor de servicio de GitHub Actions, con el esquema publicado desde el `.dacpac` con SqlPackage. Atributo propio `[SqlServerFact]` que omite la prueba si `CBL_TEST_SQL` no está definida; trait `Category=Integration`. Cubre autenticación (401/403), aislamiento entre usuarios, validación por campo, decisiones con auditoría, doble decisión (409), carrera de dos decisiones simultáneas (una sola prevalece, una sola fila de auditoría), caché fresca tras decidir, efecto de cambiar límites, bloqueo y rate limiting, formato `ProblemDetails`, y la **coincidencia del mapeo EF con el esquema** desplegado (tablas, columnas, tipos, nulabilidad, longitudes, claves, índice único) más el seed.
- **Razón**: ADR-0014 (reemplazada) y ADR-0021; constitución (VI).

## R15. CI y entrega

- **Decisión**: `ci.yml` (push y PR): compila el `.sqlproj` → `.dacpac`; levanta `mcr.microsoft.com/mssql/server` como contenedor de servicio con una contraseña temporal que no es un secreto del repositorio; publica el esquema con `SqlPackage`; define `CBL_TEST_SQL`; ejecuta pruebas unitarias e integración; en `main` genera `DeployReport` y sube el `.dacpac`. `release.yml`: un tag `v*.*.*` reconstruye, ejecuta las pruebas unitarias y crea un GitHub Release con el `.dacpac`, el zip de `dotnet publish` de la API y `SHA256SUMS.txt`, usando solo `GITHUB_TOKEN`. Sin despliegue en la nube.
- **Razón**: ADR-0016/0021/0024; constitución (Restricciones de entrega).
- **Riesgo**: el primer recorrido real de las rutas de SQL Server ocurre en CI; conviene abrir una PR borrador temprano (ADR-0021).

## R16. Configuración y secretos

- **Decisión**: `appsettings.json` sin secretos; `Jwt__Key`, `ConnectionStrings__Cbl`, `Cors__AllowedOrigins` y `Database__Provider` por variables de entorno o user-secrets; `.gitignore` cubre `appsettings.Development.json`, `.env` y `secrets.json`. Revisar `git diff --cached` antes de cada push (D6.4 del plan de DevOps).
- **Razón**: constitución (III).

## Resultado

No quedan incógnitas. Pasar a la Fase 1: [data-model.md](data-model.md), [contracts/openapi.yaml](contracts/openapi.yaml) y [quickstart.md](quickstart.md).
