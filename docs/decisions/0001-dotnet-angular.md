# ADR-0001: Solve the test with .NET + Angular
- Status: Accepted
- Date: 2026-11-11

## Context
The Makers test is written for Java/Spring Boot and Angular, but it must be solved in .NET.

## Decision
Backend in ASP.NET Core (.NET 8 LTS) and frontend in Angular. Each Spring requirement is translated to its .NET equivalent (see `docs/00-analysis.md`).

## Alternatives considered
- Spring Boot (the original stack of the PDF).

## Consequences
Theory question 6 (Spring Boot) is answered with the ASP.NET Core equivalent plus a mention of Spring.

---

## Español

### ADR-0001: Resolver la prueba en .NET + Angular
- Estado: Aceptada
- Fecha: 2026-11-11

#### Contexto
La prueba de Makers está escrita para Java/Spring Boot y Angular, pero debe resolverse en .NET.

#### Decisión
Backend en ASP.NET Core (.NET 8 LTS) y frontend en Angular. Cada requisito de Spring se traduce a su equivalente .NET (ver `docs/00-analisis.es.md`).

#### Alternativas consideradas
- Spring Boot (stack original del PDF).

#### Consecuencias
La pregunta teórica 6 (Spring Boot) se responde con el equivalente ASP.NET Core y una mención a Spring.
