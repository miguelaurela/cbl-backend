# ADR-0007: Optimistic concurrency with 409
- Status: Accepted
- Date: 2026-11-11

## Context
Two admins may decide on the same loan at the same time (race condition). It is the only real risk point.

## Decision
The `Pending → Approved/Rejected` transition is validated in the entity, with our own version column (not relying on `rowversion`), and the conflict is translated to 409 with `ProblemDetails`. Approval and audit go in a single atomic save. Cache invalidation happens after the save is confirmed.

## Alternatives considered
- Pessimistic locking.

## Consequences
An integration test fires two simultaneous decisions and checks that only one prevails.

---

## Español

### ADR-0007: Concurrencia optimista con 409
- Estado: Aceptada
- Fecha: 2026-11-11

#### Contexto
Dos administradores pueden decidir sobre el mismo préstamo a la vez (condición de carrera). Es el único punto de riesgo real.

#### Decisión
Transición `Pendiente → Aprobado/Rechazado` validada en la entidad, columna de versión propia (no depender de `rowversion`) y conflicto traducido a 409 con `ProblemDetails`. Aprobación y auditoría en un único guardado atómico. Invalidación de caché después de confirmar el guardado.

#### Alternativas consideradas
- Bloqueo pesimista.

#### Consecuencias
Una prueba de integración lanza dos decisiones simultáneas y comprueba que solo una prevalece.
