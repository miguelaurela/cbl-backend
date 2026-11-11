🇬🇧 [English](theory.md)

# Parte teórica (borrador)

> Borrador construido sobre las decisiones del proyecto. Revísalo y ajústalo con tus propias palabras antes de entregar.

## 1. Optimización en sistemas financieros
- **Concurrencia**: I/O asíncrono, sin bloqueos de hilos. Concurrencia optimista (columna de versión) en entidades con baja contención; bloqueo pesimista solo donde la contención es alta y el costo de reintento es inaceptable.
- **Caché**: en el lado de lectura (CQRS ligero), con invalidación explícita tras confirmar el guardado. Evolución: caché distribuida (Redis) al escalar horizontalmente.
- **Escalabilidad**: servicios sin estado, lecturas escalables con réplicas o modelos de lectura, colas para trabajo no crítico, idempotencia en operaciones de escritura.
- **Patrones**: Unit of Work, Repository, CQRS, Outbox.

## 2. Seguridad en APIs financieras
- **SQL Injection**: consultas parametrizadas (EF Core), nunca concatenar SQL.
- **CSRF**: no aplica con `Authorization: Bearer` en cabecera; sí aplicaría si el token viviera en cookies (SameSite, tokens anti-CSRF).
- **XSS**: salida codificada, CSP y cabeceras de seguridad; el token en memoria reduce el impacto.
- **Otros**: JWT de vida corta con validación completa, autorización por roles y por recurso (anti-IDOR), rate limiting, DTOs (anti mass assignment), HTTPS/HSTS, CORS restringido, secretos fuera del repo, errores sin detalles internos, logs sin datos sensibles.

## 3. Transacciones en sistemas distribuidos
No hay una transacción ACID única entre servicios. Se usa:
- **Saga** (orquestada o coreografiada) con compensaciones.
- **Outbox** para publicar eventos de forma atómica con el cambio local.
- **Idempotencia** con claves de idempotencia y reintentos con backoff.
- **Errores**: estados intermedios explícitos (`Pendiente`, `Fallida`, `Compensada`), timeouts, dead-letter queues y trazabilidad con correlation ID.
- Dentro de un servicio, transacción local y concurrencia optimista (409 ante conflicto).

## 4. Pruebas unitarias y de integración
- **Unitarias** (xUnit, Moq/NSubstitute, FluentAssertions): dominio, validadores y casos de uso.
- **Integración** (`WebApplicationFactory` + SQL Server como contenedor de servicio en CI, con el esquema del `.dacpac`): autenticación, autorización, aislamiento entre usuarios, decisiones, concurrencia, caché, formato de errores y coherencia entre el mapeo de EF y el esquema. En local se omiten salvo que se defina `CBL_TEST_SQL`.
- **CI**: todo corre en cada cambio.

## 5. Front-end
- **Estado**: NgRx (acciones, reducers, efectos, selectores) con slices `auth` y `loans`.
- **Autenticación**: JWT de vida corta (10 minutos) guardado solo en memoria; el interceptor añade el Bearer y cierra sesión ante 401. Trade-off: se pierde al refrescar. Evolución: refresh token en cookie `HttpOnly`.
- **Rutas**: `authGuard` y `adminGuard`; el backend siempre vuelve a validar.
- **Coherencia**: tras decidir un préstamo, los efectos recargan o actualizan el estado desde la API.

## 6. Spring Boot (equivalente ASP.NET Core)
- **a) `@SpringBootApplication`**: combina `@Configuration`, `@EnableAutoConfiguration` y `@ComponentScan`; arranca el contexto, registra beans y aplica auto-configuración condicional. En ASP.NET Core: `WebApplication.CreateBuilder` configura host, configuración, logging y DI; `builder.Build()` y `app.Run()` arman el pipeline y arrancan.
- **b) Ciclo de vida de un bean**: instanciación → inyección → `BeanPostProcessor`/`@PostConstruct` → uso → `@PreDestroy`. Se interviene con esos hooks, `@Scope` y `BeanPostProcessor`. En .NET: lifetimes `Singleton`, `Scoped`, `Transient`, `IHostedService` y `IDisposable`.
- **c) Auto-configuración sin romper convención sobre configuración**: propiedades (`application.yml`), `@ConfigurationProperties`, `@ConditionalOnMissingBean` para reemplazar solo lo necesario y `exclude` puntual. En .NET: Options Pattern y `Configure<T>`.
