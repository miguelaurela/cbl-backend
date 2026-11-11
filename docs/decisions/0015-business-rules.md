# ADR-0015: Term in months and immutable decisions
- Status: Accepted
- Date: 2026-11-11

## Context
The PDF speaks of "amount and term" and of "business rules" without defining them; the mockup shows only the amount.

## Decision
The term is included (1–60 months). Statuses `Pending`, `Approved`, `Rejected`. Decisions are made only on `Pending` loans and are final. Each decision records the admin and the date. Interest, installments and disbursements are out of scope. Further backend defaults: see ADR-0023.

## Alternatives considered
- Amount only, as in the mockup.

## Consequences
The design stays extensible for installments and interest later.

---

## Español

### ADR-0015: Plazo en meses y decisiones inmutables
- Estado: Aceptada
- Fecha: 2026-11-11

#### Contexto
El PDF habla de "monto y plazo" y de "reglas de negocio" sin definirlas; el mockup solo muestra el monto.

#### Decisión
Se incluye el plazo (1–60 meses). Estados `Pending`, `Approved`, `Rejected` (`Pendiente`, `Aprobado`, `Rechazado`). Solo se decide sobre `Pending` y la decisión es final. Cada decisión registra administrador y fecha. Intereses, cuotas y desembolsos quedan fuera de alcance. Otros valores por defecto del backend: ver ADR-0023.

#### Alternativas consideradas
- Solo monto, como en el mockup.

#### Consecuencias
El diseño queda extensible para cuotas e intereses más adelante.
