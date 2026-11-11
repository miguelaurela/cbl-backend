# ADR-0021: Integration tests run in CI only (no local Docker)
- Status: Accepted (supersedes [ADR-0014](0014-integration-tests.md); amends [ADR-0006](0006-database-strategy.md))
- Date: 2026-11-11

## Context
ADR-0014 proposed SQL Server in a container through Testcontainers, which needs Docker. The developer has no Docker locally, but GitHub Actions provides SQL Server as a service container for free.

## Decision
- Integration tests (`WebApplicationFactory` + real SQL Server) run **only in GitHub Actions**: a SQL Server service container, with the schema deployed from the `.dacpac` using `SqlPackage`. Testcontainers is not used.
- Locally they are **skipped** unless the environment variable `CBL_TEST_SQL` (SQL Server connection string) is set, so a plain `dotnet test` stays green without Docker (custom attribute plus `Category=Integration` trait).
- Unit tests and local runs keep using EF InMemory (ADR-0006).

## Alternatives considered
- Testcontainers (needs local Docker; discarded).
- LocalDB or SQLite locally (different engine, does not prove SQL Server behavior).
- Only unit tests (does not cover concurrency or the EF vs `.sqlproj` mapping).

## Consequences
SQL Server code paths are proven only in CI, so the first real run is a CI run; open a draft PR early. Anyone with a SQL Server can run the tests locally by setting `CBL_TEST_SQL`.

---

## Español

### ADR-0021: Las pruebas de integración corren solo en CI (sin Docker local)
- Estado: Aceptada (reemplaza a [ADR-0014](0014-integration-tests.md); enmienda [ADR-0006](0006-database-strategy.md))
- Fecha: 2026-11-11

#### Contexto
El ADR-0014 proponía SQL Server en contenedor con Testcontainers, que requiere Docker. El desarrollador no tiene Docker en local, pero GitHub Actions ofrece SQL Server como contenedor de servicio sin coste.

#### Decisión
- Las pruebas de integración (`WebApplicationFactory` + SQL Server real) corren **solo en GitHub Actions**: un contenedor de servicio de SQL Server, con el esquema desplegado desde el `.dacpac` mediante `SqlPackage`. No se usa Testcontainers.
- En local se **omiten** salvo que se defina la variable de entorno `CBL_TEST_SQL` (cadena de conexión a SQL Server), de modo que un `dotnet test` normal sigue en verde sin Docker (atributo propio más trait `Category=Integration`).
- Las pruebas unitarias y la ejecución local siguen usando EF InMemory (ADR-0006).

#### Alternativas consideradas
- Testcontainers (requiere Docker local; descartada).
- LocalDB o SQLite en local (motor distinto, no prueba el comportamiento de SQL Server).
- Solo pruebas unitarias (no cubre concurrencia ni el mapeo de EF frente al `.sqlproj`).

#### Consecuencias
El código que depende de SQL Server solo se prueba en CI, así que la primera ejecución real es una ejecución de CI; conviene abrir un PR borrador pronto. Quien tenga un SQL Server puede ejecutar las pruebas en local definiendo `CBL_TEST_SQL`.
