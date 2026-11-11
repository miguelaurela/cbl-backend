# ADR-0009: Short-lived JWT, no refresh token
- Status: Accepted
- Date: 2026-11-11

## Context
JWT authentication is required. The token lives only in memory in the frontend (ADR-0013).

## Decision
JWT with a **10-minute** lifetime, validating issuer, audience, signature and expiration, with minimal clock skew. When it expires, the user logs in again. Login lockout: **5 failed attempts / 15 minutes**, both values configurable.

## Alternatives considered
- Refresh token in an `HttpOnly` cookie (complicates CORS across two repos; left as an evolution).

## Consequences
The session is lost on refresh or expiry. The signing key comes from environment variables or user-secrets.

---

## Español

### ADR-0009: JWT de vida corta, sin refresh token
- Estado: Aceptada
- Fecha: 2026-11-11

#### Contexto
Se pide autenticación JWT. El token vive solo en memoria en el frontend (ADR-0013).

#### Decisión
JWT con vida de **10 minutos**, validando emisor, audiencia, firma y expiración, con tolerancia de reloj mínima. Al expirar, el usuario inicia sesión de nuevo. Bloqueo de login: **5 intentos fallidos / 15 minutos**, ambos valores configurables.

#### Alternativas consideradas
- Refresh token en cookie `HttpOnly` (complica el CORS entre dos repos; queda como evolución).

#### Consecuencias
La sesión se pierde al refrescar o expirar. La clave de firma sale de variables de entorno o user-secrets.
