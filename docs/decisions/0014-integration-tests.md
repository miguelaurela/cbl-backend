# ADR-0014: Integration tests with SQL Server in a container
- Status: Superseded by [ADR-0021](0021-integration-tests-ci-only.md)
- Date: 2026-11-11

## Context
The in-memory provider does not validate what depends on the engine.

## Decision
`WebApplicationFactory` with SQL Server in a container (for example Testcontainers) and the schema deployed from the `.dacpac`. Minimum scenarios in `docs/00-analysis.md` (11).

## Alternatives considered
- Unit tests only.

## Consequences
Slower CI, but it covers authentication, authorization, concurrency, cache and the mismatch between EF and the `.sqlproj`.

---

## Español

### ADR-0014: Pruebas de integración con SQL Server en contenedor
- Estado: Reemplazada por [ADR-0021](0021-integration-tests-ci-only.md)
- Fecha: 2026-11-11

#### Contexto
El proveedor en memoria no valida lo que depende del motor.

#### Decisión
`WebApplicationFactory` con SQL Server en contenedor (por ejemplo Testcontainers) y esquema desplegado desde el `.dacpac`. Escenarios mínimos en `docs/00-analisis.es.md` (11).

#### Alternativas consideradas
- Solo pruebas unitarias.

#### Consecuencias
CI más lento, pero cubre autenticación, autorización, concurrencia, caché y el desajuste entre EF y el `.sqlproj`.
