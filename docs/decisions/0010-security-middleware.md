# ADR-0010: Security through middleware
- Status: Accepted
- Date: 2026-11-11

## Context
Reinforcing security with middleware was requested.

## Decision
Ordered pipeline: global error handling (`ProblemDetails`), HTTPS/HSTS, security headers, restricted CORS, rate limiting (stricter on login), correlation ID and logging without sensitive data, authentication and policy-based authorization. Additionally: user id always taken from the token, DTOs, parameterized queries, Swagger only in Development.

## Alternatives considered
- Security only in controllers.

## Consequences
Cross-cutting, testable measures. The pipeline detail is in `docs/00-analysis.md`.

---

## Español

### ADR-0010: Seguridad por middleware
- Estado: Aceptada
- Fecha: 2026-11-11

#### Contexto
Se pidió reforzar la seguridad con middleware.

#### Decisión
Pipeline ordenado: manejo global de errores (`ProblemDetails`), HTTPS/HSTS, cabeceras de seguridad, CORS restringido, rate limiting (más estricto en login), correlation ID y logging sin datos sensibles, autenticación y autorización por policies. Además: id de usuario siempre desde el token, DTOs, consultas parametrizadas, Swagger solo en desarrollo.

#### Alternativas consideradas
- Seguridad solo en controladores.

#### Consecuencias
Medidas transversales y testeables. El detalle del pipeline está en `docs/00-analisis.es.md`.
