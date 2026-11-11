# ADR-0008: Loan-amount limits in a parameters table
- Status: Accepted
- Date: 2026-11-11

## Context
The minimum and maximum amount must be changeable without redeploying.

## Decision
The limits are stored in the `SystemParameters` table (seed values: 1,000 and 100,000) and validation reads them from there.

## Alternatives considered
- Fixed values in code.
- `appsettings`.

## Consequences
Changes affect only new requests. Reading the parameter may be cached, with invalidation when it is modified.

---

## Español

### ADR-0008: Límites de monto en tabla de parámetros
- Estado: Aceptada
- Fecha: 2026-11-11

#### Contexto
El monto mínimo y máximo debe poder cambiar sin redesplegar.

#### Decisión
Los límites se guardan en la tabla `SystemParameters` (valores semilla: 1.000 y 100.000) y la validación los lee de ahí.

#### Alternativas consideradas
- Valores fijos en código.
- `appsettings`.

#### Consecuencias
Los cambios afectan solo a nuevas solicitudes. La lectura del parámetro puede cachearse con invalidación al modificarlo.
