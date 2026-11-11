# ADR-0006: EF InMemory for local and unit tests; real SQL Server in CI
- Status: Accepted (amended by [ADR-0021](0021-integration-tests-ci-only.md): in CI, SQL Server runs as a GitHub Actions service container)
- Date: 2026-11-11

## Context
An in-memory .NET database with a SQL Server model was requested. The InMemory provider supports neither real transactions nor relational constraints nor `rowversion`.

## Decision
InMemory only for fast local runs and unit tests. Everything that depends on the engine (transactions, constraints, concurrency) is validated in CI against a real SQL Server (service container, see ADR-0021).

## Alternatives considered
- InMemory only.
- In-memory SQLite.

## Consequences
Atomicity is not guaranteed by the engine locally; it is verified in CI. Switching to SQL Server only requires changing the provider and the connection string.

---

## Español

### ADR-0006: EF en memoria para local y unitarias; SQL Server real en CI
- Estado: Aceptada (enmendada por [ADR-0021](0021-integration-tests-ci-only.md): en CI, SQL Server corre como contenedor de servicio de GitHub Actions)
- Fecha: 2026-11-11

#### Contexto
Se pidió base de datos en memoria de .NET con modelo de SQL Server. El proveedor InMemory no soporta transacciones reales ni restricciones relacionales ni `rowversion`.

#### Decisión
InMemory solo para ejecución local rápida y pruebas unitarias. Todo lo que depende del motor (transacciones, restricciones, concurrencia) se valida en CI contra SQL Server real (contenedor de servicio, ver ADR-0021).

#### Alternativas consideradas
- Solo InMemory.
- SQLite en memoria.

#### Consecuencias
La atomicidad no la garantiza el motor en local; se verifica en CI. Cambiar a SQL Server requiere solo cambiar el proveedor y la cadena de conexión.
