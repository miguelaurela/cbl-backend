# ADR-0011: Read-side cache with `IMemoryCache`
- Status: Accepted
- Date: 2026-11-11

## Context
The PDF asks for a cache for repeated queries on loan status.

## Decision
`IMemoryCache` behind an interface, on the read side only, with explicit invalidation after a decision save is confirmed. (TTL: see ADR-0023.)

## Alternatives considered
- Redis (left as an evolution for horizontal scaling).
- No cache.

## Consequences
An integration test checks that the cache returns up-to-date data after a decision.

---

## Español

### ADR-0011: Caché en lectura con `IMemoryCache`
- Estado: Aceptada
- Fecha: 2026-11-11

#### Contexto
El PDF pide caché para consultas repetitivas sobre el estado de los préstamos.

#### Decisión
`IMemoryCache` detrás de una interfaz, solo en el lado de lectura, con invalidación explícita tras confirmar el guardado de una decisión. (TTL: ver ADR-0023.)

#### Alternativas consideradas
- Redis (queda como evolución para escalar horizontalmente).
- Sin caché.

#### Consecuencias
Un test de integración comprueba que la caché devuelve datos actualizados tras decidir.
