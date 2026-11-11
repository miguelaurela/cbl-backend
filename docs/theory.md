🇪🇸 [Español](teoria.es.md)

# Theory part (draft)

> Draft built on the project decisions. Review it and adjust it in your own words before submitting.

## 1. Optimization in financial systems
- **Concurrency**: asynchronous I/O, no thread blocking. Optimistic concurrency (version column) on low-contention entities; pessimistic locking only where contention is high and the retry cost is unacceptable.
- **Cache**: on the read side (light CQRS), with explicit invalidation after the save is confirmed. Evolution: distributed cache (Redis) when scaling horizontally.
- **Scalability**: stateless services, reads that scale with replicas or read models, queues for non-critical work, idempotency in write operations.
- **Patterns**: Unit of Work, Repository, CQRS, Outbox.

## 2. Security in financial APIs
- **SQL Injection**: parameterized queries (EF Core), never concatenate SQL.
- **CSRF**: does not apply with `Authorization: Bearer` in a header; it would apply if the token lived in cookies (SameSite, anti-CSRF tokens).
- **XSS**: encoded output, CSP and security headers; keeping the token in memory reduces the impact.
- **Others**: short-lived JWT with full validation, role-based and resource-based authorization (anti-IDOR), rate limiting, DTOs (anti mass assignment), HTTPS/HSTS, restricted CORS, secrets outside the repo, errors without internal details, logs without sensitive data.

## 3. Transactions in distributed systems
There is no single ACID transaction across services. We use:
- **Saga** (orchestrated or choreographed) with compensations.
- **Outbox** to publish events atomically with the local change.
- **Idempotency** with idempotency keys and retries with backoff.
- **Errors**: explicit intermediate states (`Pending`, `Failed`, `Compensated`), timeouts, dead-letter queues and traceability with a correlation ID.
- Inside a single service, a local transaction and optimistic concurrency (409 on conflict).

## 4. Unit and integration tests
- **Unit** (xUnit, Moq/NSubstitute, FluentAssertions): domain, validators and use cases.
- **Integration** (`WebApplicationFactory` + SQL Server as a service container in CI, with the `.dacpac` schema): authentication, authorization, isolation between users, decisions, concurrency, cache, error format and consistency between the EF mapping and the schema. Locally they are skipped unless `CBL_TEST_SQL` is set.
- **CI**: everything runs on every change.

## 5. Front-end
- **State**: NgRx (actions, reducers, effects, selectors) with `auth` and `loans` slices.
- **Authentication**: short-lived JWT (10 minutes) kept only in memory; the interceptor adds the Bearer and logs out on 401. Trade-off: it is lost on refresh. Evolution: refresh token in an `HttpOnly` cookie.
- **Routes**: `authGuard` and `adminGuard`; the backend always validates again.
- **Consistency**: after deciding a loan, effects reload or update the state from the API.

## 6. Spring Boot (ASP.NET Core equivalent)
- **a) `@SpringBootApplication`**: combines `@Configuration`, `@EnableAutoConfiguration` and `@ComponentScan`; it starts the context, registers beans and applies conditional auto-configuration. In ASP.NET Core: `WebApplication.CreateBuilder` sets up host, configuration, logging and DI; `builder.Build()` and `app.Run()` assemble the pipeline and start.
- **b) Bean lifecycle**: instantiation → injection → `BeanPostProcessor`/`@PostConstruct` → use → `@PreDestroy`. You intervene with those hooks, `@Scope` and `BeanPostProcessor`. In .NET: `Singleton`, `Scoped`, `Transient` lifetimes, `IHostedService` and `IDisposable`.
- **c) Auto-configuration without breaking convention over configuration**: properties (`application.yml`), `@ConfigurationProperties`, `@ConditionalOnMissingBean` to replace only what is needed and a targeted `exclude`. In .NET: the Options Pattern and `Configure<T>`.
