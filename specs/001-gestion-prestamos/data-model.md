# Modelo de datos (Fase 1): gestión de préstamos bancarios – backend

**Rama**: `001-gestion-prestamos` | **Fecha**: 2026-11-11 | **Plan**: [plan.md](plan.md) | **Especificación**: [spec.md](spec.md)

El esquema SQL de `db/CBL.Database` es la fuente de verdad; `OnModelCreating` lo replica de forma explícita y una prueba de integración en CI verifica que coincidan. Los nombres de tabla son `Users`, `Loans`, `LoanDecisionAudit` y `SystemParameters`.

## Entidades

### User (`Users`)

| Campo | Tipo | Reglas |
|---|---|---|
| `Id` | `uniqueidentifier` (Guid) | Clave primaria. |
| `Email` | `nvarchar(256)` | Obligatorio, **único**, guardado normalizado en minúsculas; la búsqueda también se normaliza (el correo no distingue mayúsculas). |
| `FullName` | `nvarchar(200)` | Obligatorio. |
| `PasswordHash` | `nvarchar(500)` | Obligatorio; hash PBKDF2, nunca se expone en un DTO. |
| `Role` | `nvarchar(20)` | `CHECK (Role IN ('Admin','Borrower'))`. Un único rol por usuario. |
| `IsActive` | `bit` | Por defecto `1`. La desactivación es lógica; nunca se elimina a un usuario con historial. |
| `CreatedAt` | `datetime2(0)` | UTC. |

Reglas: contraseña ≥ 8 caracteres al crear o restablecer por la API (el seed no pasa por la API). Un admin no puede dejar `IsActive = false` en su propia cuenta (403). El contador de intentos fallidos de login **no** se guarda aquí (ver plan, D4).

### Loan (`Loans`)

| Campo | Tipo | Reglas |
|---|---|---|
| `Id` | `uniqueidentifier` | Clave primaria. |
| `BorrowerId` | `uniqueidentifier` | FK a `Users`; siempre sale del token. |
| `Amount` | `decimal(18,2)` | `CHECK (Amount > 0)`; dentro de los límites vigentes al solicitar o editar. |
| `TermMonths` | `int` | `CHECK (TermMonths BETWEEN 1 AND 60)`. |
| `Status` | `nvarchar(20)` | `CHECK (Status IN ('Pending','Approved','Rejected'))`; por defecto `Pending`. |
| `CreatedAt` | `datetime2(0)` | UTC. |
| `DecidedByUserId` | `uniqueidentifier` NULL | FK a `Users` (el admin que decidió). |
| `DecidedAt` | `datetime2(0)` NULL | UTC. Nulos mientras esté `Pending`. |
| `Version` | `int` | `NOT NULL DEFAULT 1`; token de concurrencia, lo incrementa la entidad en cada cambio. |

Índices: `(BorrowerId, Status)` para "mis préstamos"; `(Status, CreatedAt)` para la lista del admin filtrada por estado. Borrado de claves foráneas en modo `Restrict`.

### LoanDecisionAudit (`LoanDecisionAudit`)

Registro de solo inserción de cada decisión (quién y cuándo).

| Campo | Tipo | Reglas |
|---|---|---|
| `Id` | `uniqueidentifier` | Clave primaria. |
| `LoanId` | `uniqueidentifier` | FK a `Loans`; índice. |
| `AdminUserId` | `uniqueidentifier` | FK a `Users`. |
| `Decision` | `nvarchar(20)` | `CHECK (Decision IN ('Approved','Rejected'))`. |
| `PreviousStatus` | `nvarchar(20)` | Siempre `Pending` (las decisiones solo parten de ahí). |
| `DecidedAt` | `datetime2(0)` | UTC; igual que `Loans.DecidedAt`. |
| `CorrelationId` | `nvarchar(64)` NULL | Identificador de la petición. |

Sin métodos de actualización en el dominio. Se escribe en el **mismo** `SaveChangesAsync` que el cambio de estado del préstamo (atomicidad, FR-016).

### SystemParameter (`SystemParameters`)

| Campo | Tipo | Reglas |
|---|---|---|
| `Key` | `nvarchar(100)` | Clave primaria. Valores usados: `LoanAmountMin`, `LoanAmountMax`. |
| `Value` | `nvarchar(500)` | Valor como texto (decimal invariante). |
| `UpdatedAt` | `datetime2(0)` | UTC. |
| `UpdatedByUserId` | `uniqueidentifier` NULL | FK a `Users`; nulo para el valor inicial. |

Seed (idempotente, `MERGE`): `LoanAmountMin = 1000` y `LoanAmountMax = 100000`. Validación al modificar: mínimo > 0 y máximo > mínimo. Cambiarlos solo afecta a solicitudes nuevas (FR-024).

## Datos iniciales (post-despliegue)

| Dato | Valor |
|---|---|
| Prestamista | `usuario@test.com`, rol `Borrower`, contraseña `123` (hash PBKDF2 precalculado; **solo de ejemplo**) |
| Administrador | `admin@test.com`, rol `Admin`, contraseña `123` (ídem) |
| Límites | mínimo 1.000, máximo 100.000 |

El seeder de código para EF InMemory (solo Development) inserta los mismos datos.

## Relaciones

```text
User 1 ──< Loan            (BorrowerId)
User 1 ──< Loan            (DecidedByUserId, opcional)
Loan 1 ──< LoanDecisionAudit
User 1 ──< LoanDecisionAudit (AdminUserId)
SystemParameter             (independiente; clave-valor)
```

## Transiciones de estado del préstamo

```text
            Approve(adminId)            Reject(adminId)
 Pending ─────────────────► Approved   Pending ─────────────────► Rejected
    │
    └── (Update / Delete permitidos solo en este estado)

 Approved y Rejected: terminales e inmutables (sin transiciones de salida).
```

- Solo existe `Pending → Approved` y `Pending → Rejected`. Cualquier otra transición lanza `InvalidLoanTransitionException` → 409.
- Cada transición: fija `DecidedByUserId` y `DecidedAt`, incrementa `Version` y añade una fila a `LoanDecisionAudit`, todo en un único guardado.
- Si dos decisiones compiten, la segunda falla por versión desactualizada → `ConcurrencyConflictException` → 409.

## Reglas de validación (de los requisitos)

| Regla | Origen | Dónde se aplica |
|---|---|---|
| Monto > 0 y con 2 decimales como máximo | FR-008 | Validador y dominio |
| Monto dentro de los límites vigentes | FR-008, FR-024 | Caso de uso `RequestLoan` / `UpdateLoan` (lee `SystemParameters` con caché) |
| Plazo entero entre 1 y 60 | FR-009 | Validador y dominio |
| Solo un Borrower solicita | FR-006 | Policy `BorrowerOnly` |
| Editar/eliminar propio solo si `Pending`; Admin elimina solo `Pending` | FR-012 | Dominio (`Loan`) y caso de uso (dueño/rol) |
| Decidir solo si `Pending`; decisión final | FR-014, FR-015 | Dominio (`Loan.Approve/Reject`) |
| Contraseña ≥ 8 y correo único | FR-021 | Validador y restricción `UNIQUE` |
| Un admin no se desactiva a sí mismo | FR-020 | Caso de uso `UpdateUser` |
| Límites: mínimo > 0 y máximo > mínimo | FR-024 | Validador de `UpdateLoanLimits` |

## Nota de mapeo EF

`CblDbContext` mapea tablas, columnas, longitudes, precisión `decimal(18,2)`, enumeraciones como cadena, índices, claves foráneas (`Restrict`), el índice único de `Email` y `Version` con `IsConcurrencyToken()`. Cualquier divergencia con el `.sqlproj` la detecta la prueba de coincidencia en CI.
