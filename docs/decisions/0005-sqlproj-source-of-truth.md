# ADR-0005: `.sqlproj` as the source of truth, no EF migrations
- Status: Accepted
- Date: 2026-11-11

## Context
We want a SQL database project holding the definitions, buildable and deployable directly by CI.

## Decision
`/db/CBL.Database` (Microsoft.Build.Sql) defines tables, constraints, indexes, the version column and a post-deployment script with seed data. EF Core only maps through `OnModelCreating`. EF migrations are not used. An integration test detects mismatches between both.

## Alternatives considered
- EF Core migrations.

## Consequences
Risk of two descriptions of the schema, mitigated by the mismatch test in CI. The seed password hashes are stored precomputed. No stored procedures are written for approvals: the logic lives in the domain.

---

## Español

### ADR-0005: `.sqlproj` como fuente de verdad, sin migraciones de EF
- Estado: Aceptada
- Fecha: 2026-11-11

#### Contexto
Se quiere un proyecto de base de datos SQL con las definiciones, compilable y desplegable directamente por CI.

#### Decisión
`/db/CBL.Database` (Microsoft.Build.Sql) define tablas, restricciones, índices, columna de versión y un script de post-despliegue con datos semilla. EF Core solo mapea mediante `OnModelCreating`. No se usan migraciones de EF. Un test de integración detecta desajustes entre ambos.

#### Alternativas consideradas
- Migraciones de EF Core.

#### Consecuencias
Riesgo de dos descripciones del esquema, mitigado con la prueba de desajuste en CI. Los hashes de las contraseñas semilla se guardan ya calculados. No se escriben procedimientos almacenados para las aprobaciones: la lógica vive en el dominio.
