# ADR-0022: Docs and repo conventions
- Status: Accepted
- Date: 2026-11-11

## Context
The project is a technical test for a Spanish-speaking team, with English-language code, two repos and two AI agents available.

## Decision
- **Bilingual docs**: the English file is the default (`README.md`, `docs/00-analysis.md`, `docs/theory.md`); the Spanish twin uses `.es.md` (`README.es.md`, `docs/00-analisis.es.md`, `docs/teoria.es.md`). Each file links to its twin at the top. ADRs are a single bilingual file (English section, then a `## Español` section).
- **Spec Kit artifacts** (constitution, spec, plan, tasks) are written in Spanish; the prompts are kept in `docs/speckit-log.md`.
- **Repos**: `cbl-backend` and `cbl-frontend`, both public, under the GitHub user `miguelaurela`.
- **Spec Kit** is initialized for two agents (Claude Code and GitHub Copilot). Claude Code drives the phases; Copilot is only a reviewer.
- **Code and identifiers are in English.**

## Alternatives considered
- Spanish-only or English-only docs.
- Spec Kit artifacts in English (tooling is English, but reviewers are Spanish-speaking).
- A single agent only.

## Consequences
Every doc change must be made in both languages. Spec Kit artifacts are not duplicated, because generated files are tool input.

---

## Español

### ADR-0022: Convenciones de documentación y repositorios
- Estado: Aceptada
- Fecha: 2026-11-11

#### Contexto
El proyecto es una prueba técnica para un equipo hispanohablante, con código en inglés, dos repositorios y dos agentes de IA disponibles.

#### Decisión
- **Documentación bilingüe**: el archivo en inglés es el predeterminado (`README.md`, `docs/00-analysis.md`, `docs/theory.md`); su gemelo en español usa `.es.md` (`README.es.md`, `docs/00-analisis.es.md`, `docs/teoria.es.md`). Cada archivo enlaza a su gemelo al inicio. Los ADR son un único archivo bilingüe (sección en inglés y luego una sección `## Español`).
- **Artefactos de Spec Kit** (constitution, spec, plan, tasks) redactados en español; los prompts se guardan en `docs/speckit-log.md`.
- **Repositorios**: `cbl-backend` y `cbl-frontend`, ambos públicos, bajo el usuario de GitHub `miguelaurela`.
- **Spec Kit** inicializado para dos agentes (Claude Code y GitHub Copilot). Claude Code conduce las fases; Copilot solo revisa.
- **El código y los identificadores van en inglés.**

#### Alternativas consideradas
- Documentación solo en español o solo en inglés.
- Artefactos de Spec Kit en inglés (las herramientas están en inglés, pero los revisores hablan español).
- Un único agente.

#### Consecuencias
Todo cambio de documentación se hace en ambos idiomas. Los artefactos de Spec Kit no se duplican, porque los archivos generados son entrada de la herramienta.
