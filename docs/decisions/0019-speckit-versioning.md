# ADR-0019: Spec Kit versioned in the repository
- Status: Accepted
- Date: 2026-11-11

## Context
It was requested that everything related to Spec Kit stays in the repo, together with how it was used and which decisions were made.

## Decision
`.specify/`, `specs/`, the agent commands and the documentation in `docs/` are versioned. One commit per phase (`docs(spec): constitution`, `specify`, `clarify`, `plan`), feature branches merged with `--no-ff` or by PR, no history rewriting before delivery. Decision changes are recorded with a new ADR.

## Alternatives considered
- Leave `.specify/` and `specs/` out of version control.

## Consequences
The history tells the process. Care is needed to avoid committing secrets in generated artifacts.

---

## Español

### ADR-0019: Spec Kit versionado en el repositorio
- Estado: Aceptada
- Fecha: 2026-11-11

#### Contexto
Se pidió que todo lo de Spec Kit quede en el repo, con cómo se usó y qué decisiones se tomaron.

#### Decisión
Se versionan `.specify/`, `specs/`, los comandos del agente y la documentación en `docs/`. Un commit por fase (`docs(spec): constitution`, `specify`, `clarify`, `plan`), ramas por feature fusionadas con `--no-ff` o por PR, sin reescribir el historial antes de entregar. Los cambios de decisión se registran con un ADR nuevo.

#### Alternativas consideradas
- Dejar fuera `.specify/` y `specs/` del control de versiones.

#### Consecuencias
El historial cuenta el proceso. Hay que evitar subir secretos en los artefactos generados.
