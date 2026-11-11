# ADR-0003: Clean architecture with 4 projects
- Status: Accepted
- Date: 2026-11-11

## Context
The PDF asks for clean or hexagonal architecture.

## Decision
Projects Domain, Application, Infrastructure and Api (plus Tests). Domain has no dependencies; Application defines ports (repositories, Unit of Work, cache); Infrastructure implements them with EF Core.

## Alternatives considered
- Simple layers in a single project.
- Vertical slices.

## Consequences
It allows changing the database provider without touching domain or application. A bit more initial structure for a 2-hour test.

---

## Español

### ADR-0003: Arquitectura limpia con 4 proyectos
- Estado: Aceptada
- Fecha: 2026-11-11

#### Contexto
El PDF pide arquitectura limpia o hexagonal.

#### Decisión
Proyectos Domain, Application, Infrastructure y Api (más Tests). Domain sin dependencias; Application define puertos (repositorios, Unit of Work, caché); Infrastructure los implementa con EF Core.

#### Alternativas consideradas
- Capas simples en un solo proyecto.
- Vertical slices.

#### Consecuencias
Permite cambiar el proveedor de base de datos sin tocar dominio ni aplicación. Algo más de estructura inicial para una prueba de 2 horas.
