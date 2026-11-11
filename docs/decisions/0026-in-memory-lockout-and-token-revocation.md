# ADR-0026: Login lockout and token revocation kept in process memory
- Status: Accepted (refines [ADR-0009](0009-short-lived-jwt.md) and [ADR-0018](0018-passwords-and-login.md))
- Date: 2026-11-11

## Context
Two requirements of the spec cannot be met by a purely stateless 10-minute JWT:
- **Lockout per email, whether or not the email exists** (clarification of the spec): after 5 failed attempts with the same email the next attempts are blocked for 15 minutes, with a message that does not reveal if the email exists. A counter stored in the `Users` table cannot track an email that has no row.
- **Logout** (FR-004): after signing out, the token must stop working in the API even though it is still within its 10 minutes of life.

The scope is a technical test with a single instance and short-lived tokens.

## Decision
- `ILoginAttemptTracker`: an in-memory dictionary keyed by the normalized email (case-insensitive), with an injected clock. 5 failures inside the 15-minute window (both values configurable: `Auth:MaxFailedAttempts`, `Auth:LockoutMinutes`) block the email for 15 minutes (HTTP 423 with `Retry-After`). Memory is bounded: entries expire with the window and there is a maximum of 10,000 entries (the least recent ones are evicted).
- `ITokenRevocationStore`: an in-memory map `jti → keep-until`. `POST /api/auth/logout` revokes the `jti` of the current token; the JwtBearer `OnTokenValidated` event rejects (401) any token whose `jti` is revoked. Each entry is kept until `exp` + the 30 s clock skew JwtBearer still accepts + 1 minute, then purged, so the store does not grow without limit.
- Both are registered as singletons in `CBL.Infrastructure`; the use cases depend only on the ports defined in `CBL.Application`.

## Alternatives considered
- Lockout columns in `Users` (`FailedLoginCount`, `LockoutUntil`): cannot track unknown emails and would reveal which emails exist.
- A database table of attempts and revoked tokens: more schema, more writes on the login path, no real benefit with one instance.
- Redis (or another shared store): the natural evolution for several instances; out of scope for the test.
- Refresh token with server-side revocation: already discarded by ADR-0009.
- Only discarding the token in the client: leaves it valid in the API for up to 10 minutes, which breaks the logout acceptance scenario.
- Counting failures per email **and** origin: resists targeted lockouts better but is more complex; the user chose per email (clarification of the spec).

## Consequences
- **Lost on restart and not shared between instances.** Acceptable with one instance and 10-minute tokens; documented in the README. Scaling out requires moving both ports to a shared store (Redis or the database) without touching the use cases.
- **Accepted risk – targeted lockout:** anyone who knows an email can lock it for 15 minutes with 5 failed attempts. It is mitigated by the strict per-IP rate limit on `/api/auth/login` (20 per minute by default), the short window, and the fact that locking is per email so the message never reveals whether it exists. A per-email-and-origin counter is the upgrade path if this becomes a problem.
- Number ADR-0026 is now used in the backend; since numbering is shared with `cbl-frontend`, its next ADR must be 0027 or higher.

---

## Español

### ADR-0026: Bloqueo de login y revocación de tokens en memoria del proceso
- Estado: Aceptada (precisa [ADR-0009](0009-short-lived-jwt.md) y [ADR-0018](0018-passwords-and-login.md))
- Fecha: 2026-11-11

#### Contexto
Dos requisitos del spec no se pueden cumplir con un JWT de 10 minutos puramente sin estado:
- **Bloqueo por correo, exista o no el correo** (aclaración del spec): tras 5 intentos fallidos con el mismo correo se bloquean los siguientes durante 15 minutos, con un mensaje que no revela si el correo existe. Un contador guardado en la tabla `Users` no puede seguir a un correo que no tiene fila.
- **Cierre de sesión** (FR-004): tras cerrar sesión, el token debe dejar de funcionar en la API aunque todavía esté dentro de sus 10 minutos de vida.

El alcance es una prueba técnica con una sola instancia y tokens de vida corta.

#### Decisión
- `ILoginAttemptTracker`: un diccionario en memoria con el correo normalizado como clave (sin distinguir mayúsculas) y un reloj inyectado. 5 fallos dentro de la ventana de 15 minutos (ambos valores configurables: `Auth:MaxFailedAttempts`, `Auth:LockoutMinutes`) bloquean el correo 15 minutos (HTTP 423 con `Retry-After`). La memoria está acotada: las entradas caducan con la ventana y hay un máximo de 10.000 entradas (se descartan las menos recientes).
- `ITokenRevocationStore`: un mapa en memoria `jti → conservar hasta`. `POST /api/auth/logout` revoca el `jti` del token actual; el evento `OnTokenValidated` de JwtBearer rechaza (401) cualquier token cuyo `jti` esté revocado. Cada entrada se conserva hasta `exp` + los 30 s de tolerancia de reloj con los que JwtBearer aún acepta el token + 1 minuto, y luego se purga, de modo que el almacén no crece sin límite.
- Ambos se registran como singleton en `CBL.Infrastructure`; los casos de uso solo dependen de los puertos definidos en `CBL.Application`.

#### Alternativas consideradas
- Columnas de bloqueo en `Users` (`FailedLoginCount`, `LockoutUntil`): no pueden seguir correos desconocidos y revelarían qué correos existen.
- Una tabla de intentos y de tokens revocados en base de datos: más esquema, más escrituras en el camino del login y ningún beneficio real con una instancia.
- Redis (u otro almacén compartido): es la evolución natural para varias instancias; fuera del alcance de la prueba.
- Refresh token con revocación en el servidor: ya descartado por el ADR-0009.
- Solo descartar el token en el cliente: lo deja válido en la API hasta 10 minutos, lo que incumple el escenario de aceptación del cierre de sesión.
- Contar los fallos por correo **y** origen: resiste mejor un bloqueo malintencionado, pero es más complejo; el usuario eligió por correo (aclaración del spec).

#### Consecuencias
- **Se pierden al reiniciar y no se comparten entre instancias.** Es aceptable con una instancia y tokens de 10 minutos; está documentado en el README. Escalar horizontalmente exige mover ambos puertos a un almacén compartido (Redis o la base de datos) sin tocar los casos de uso.
- **Riesgo aceptado – bloqueo dirigido:** cualquiera que conozca un correo puede bloquearlo 15 minutos con 5 intentos fallidos. Se mitiga con el límite de tasa estricto por IP en `/api/auth/login` (20 por minuto por defecto), la ventana corta y el hecho de que el bloqueo es por correo, así que el mensaje nunca revela si existe. Un contador por correo y origen es la vía de mejora si esto se convierte en un problema.
- El número ADR-0026 ya está usado en el backend; como la numeración se comparte con `cbl-frontend`, su próximo ADR debe ser 0027 o superior.
