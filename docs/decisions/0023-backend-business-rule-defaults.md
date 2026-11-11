# ADR-0023: Backend business-rule defaults
- Status: Accepted
- Date: 2026-11-11

## Context
The PDF leaves several business rules undefined. Defaults are needed so the spec, tests and UI agree.

## Decision
- Roles are exactly `Admin` and `Borrower`.
- A Borrower can edit or delete **only their own** loan, and only while it is `Pending`.
- An Admin can delete `Pending` loans only.
- Decided loans (`Approved`/`Rejected`) are immutable.
- Only a Borrower can `POST /api/loans`; an Admin cannot request loans.
- Users created by an Admin need a password of at least 8 characters. The seed password `123` is documented as sample-only.
- Loan-status cache: TTL of 5 minutes (absolute) plus explicit invalidation after a decision (ADR-0011).

## Alternatives considered
- Admin may also request or edit loans (blurs the roles).
- Deleting decided loans (breaks audit).
- Sliding expiration for the cache (can serve stale data indefinitely).

## Consequences
The rules are enforced in the domain and covered by unit tests. The seed password does not meet the 8-character rule; it is inserted directly by the seed script, not through the API.

---

## Español

### ADR-0023: Valores por defecto de reglas de negocio del backend
- Estado: Aceptada
- Fecha: 2026-11-11

#### Contexto
El PDF deja varias reglas de negocio sin definir. Se necesitan valores por defecto para que la especificación, las pruebas y la interfaz coincidan.

#### Decisión
- Los roles son exactamente `Admin` y `Borrower` (Prestamista).
- Un Borrower puede editar o eliminar **solo su propio** préstamo, y solo mientras esté `Pending`.
- Un Admin solo puede eliminar préstamos `Pending`.
- Los préstamos decididos (`Approved`/`Rejected`) son inmutables.
- Solo un Borrower puede hacer `POST /api/loans`; un Admin no puede solicitar préstamos.
- Los usuarios creados por un Admin necesitan una contraseña de al menos 8 caracteres. La contraseña semilla `123` se documenta como solo de ejemplo.
- Caché del estado de préstamos: TTL de 5 minutos (absoluto) más invalidación explícita tras una decisión (ADR-0011).

#### Alternativas consideradas
- Que el Admin también pueda solicitar o editar préstamos (difumina los roles).
- Eliminar préstamos decididos (rompe la auditoría).
- Expiración deslizante de la caché (puede servir datos obsoletos indefinidamente).

#### Consecuencias
Las reglas se aplican en el dominio y se cubren con pruebas unitarias. La contraseña semilla no cumple la regla de 8 caracteres; la inserta directamente el script de seed, no la API.
