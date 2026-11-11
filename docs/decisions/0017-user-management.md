# ADR-0017: User management by the administrator
- Status: Accepted
- Date: 2026-11-11

## Context
The PDF asks for user CRUD but does not define public registration.

## Decision
The administrator manages users (create, read, update, deactivate) and also manages the loan-amount limits (ADR-0008). The UI has an Admin users screen. There is seed data (`usuario@test.com` and `admin@test.com`). There is no self-registration or password recovery. Deactivation is logical; users with loans are never deleted.

## Alternatives considered
- Public user registration.

## Consequences
Smaller attack surface and smaller scope. The only roles are `Admin` and `Borrower`.

---

## Español

### ADR-0017: Gestión de usuarios por el administrador
- Estado: Aceptada
- Fecha: 2026-11-11

#### Contexto
El PDF pide CRUD de usuarios pero no define el registro público.

#### Decisión
El administrador gestiona usuarios (crear, consultar, actualizar, desactivar) y también gestiona los límites de monto de préstamo (ADR-0008). La interfaz tiene una pantalla de usuarios para el Admin. Hay datos semilla (`usuario@test.com` y `admin@test.com`). No hay auto-registro ni recuperación de contraseña. La desactivación es lógica, no se borran usuarios con préstamos.

#### Alternativas consideradas
- Registro público de usuarios.

#### Consecuencias
Menos superficie de ataque y menos alcance. Los únicos roles son `Admin` y `Borrower` (Prestamista).
