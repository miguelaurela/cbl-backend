# ADR-0004: Light CQRS without MediatR
- Status: Accepted
- Date: 2026-11-11

## Context
The PDF does not require CQRS and time is limited. Still, separating writes and reads adds value (the cache lives only on the read side).

## Decision
Separate, by convention, commands (`RequestLoan`, `ApproveLoan`, `RejectLoan`) and queries (`GetMyLoans`, `GetAllLoans`, `GetLoanStatus`) in application services, with no bus and no MediatR.

## Alternatives considered
- Full CQRS with MediatR (discarded because of scope and because its license model changed in 2025; worth reviewing before adopting it).
- CRUD services without separation.

## Consequences
Full CQRS is mentioned as a concept in the theory part (questions 1 and 3); it is not implemented.

---

## Español

### ADR-0004: CQRS ligero sin MediatR
- Estado: Aceptada
- Fecha: 2026-11-11

#### Contexto
El PDF no exige CQRS y el tiempo es limitado. Aun así, separar escritura y lectura aporta valor (la caché vive solo en lectura).

#### Decisión
Separar por convención comandos (`RequestLoan`, `ApproveLoan`, `RejectLoan`) y consultas (`GetMyLoans`, `GetAllLoans`, `GetLoanStatus`) en servicios de aplicación, sin bus ni MediatR.

#### Alternativas consideradas
- CQRS completo con MediatR (se descartó por alcance y porque su modelo de licencia cambió en 2025; conviene revisarlo antes de adoptarlo).
- Servicios CRUD sin separación.

#### Consecuencias
CQRS completo se menciona como concepto en la teoría (preguntas 1 y 3), no se implementa.
