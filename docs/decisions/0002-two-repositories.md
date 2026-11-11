# ADR-0002: Two repositories
- Status: Accepted
- Date: 2026-11-11

## Context
There is a .NET backend and an Angular frontend.

## Decision
One repository per part (`cbl-backend`, `cbl-frontend`), each with its own Spec Kit. The contract between them is the REST API documented with OpenAPI.

## Alternatives considered
- Monorepo (initial proposal).

## Consequences
CORS must be restricted to the frontend origin. Each repo keeps its own constitution, spec and log. The frontend reuses user stories 1–5 and 7.

---

## Español

### ADR-0002: Dos repositorios
- Estado: Aceptada
- Fecha: 2026-11-11

#### Contexto
Hay un backend .NET y un frontend Angular.

#### Decisión
Un repositorio por parte (`cbl-backend`, `cbl-frontend`), cada uno con su propio Spec Kit. El contrato entre ambos es la API REST documentada con OpenAPI.

#### Alternativas consideradas
- Monorepo (propuesta inicial).

#### Consecuencias
Se requiere CORS restringido al origen del frontend. Cada repo mantiene su constitution, spec y bitácora. El frontend reutiliza las historias de usuario 1–5 y 7.
