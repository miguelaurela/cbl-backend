# Resultados de la validación del quickstart (T111)

**Fecha**: 2026-11-11 | **Guía**: [quickstart.md](quickstart.md) | **Entorno**: API publicada en local (`dotnet run`, Development, EF InMemory), peticiones HTTP reales con PowerShell; el límite de tasa del login se subió a 1000/min solo para esta ejecución.

Resultado: **20 de 20 escenarios correctos**. Dos de ellos no se pueden demostrar en vivo con esta configuración y se verificaron con pruebas automáticas (columna "Cómo").

| # | Escenario | Resultado | Cómo |
|---|---|---|---|
| 1 | Login correcto: 200, token, `expiresAt` en 10 min, rol Borrower | OK | En vivo |
| 2 | Sin token → 401 `application/problem+json` | OK | En vivo |
| 3 | Solicitud válida → 201 con estado `Pending` | OK | En vivo |
| 4 | Monto 0 y plazo 61 → 422 con `amount` y `termMonths` | OK | En vivo |
| 5 | Un Admin solicita → 403 | OK | En vivo |
| 6 | Otro prestamista no ve el préstamo ajeno (404) y su lista está vacía | OK | En vivo |
| 7 | El Admin lista `Pending` con prestamista, monto y estado | OK | En vivo |
| 8 | Aprobar → 200 con quién (`decidedById`, nombre) y cuándo (`decidedAt`) | OK | En vivo |
| 9 | El estado consultado tras decidir es el nuevo | OK | En vivo |
| 10 | Decidir de nuevo → 409 y el préstamo no cambia | OK | En vivo |
| 11 | Un Borrower aprueba → 403 | OK | En vivo |
| 12 | Carrera de dos decisiones simultáneas: una prevalece, una sola fila de auditoría | OK | **No aplica en InMemory**; `ConcurrencyRaceTests` contra SQL Server (LocalDB y CI) |
| 13 | Cambiar los límites aplica a la siguiente solicitud (1500 → 422, 2000 → 201) | OK | En vivo |
| 14 | Contraseña de 7 → 422, de 8 → 201, correo duplicado (otra capitalización) → 409 | OK | En vivo |
| 15 | Desactivar: no inicia sesión y el historial se conserva | OK | En vivo |
| 16 | Un Admin se desactiva o se cambia el rol → 403 | OK | En vivo |
| 17 | 5 fallos → 423 con `Retry-After`, también con un correo inexistente | OK | En vivo |
| 18 | La sesión dura 10 minutos | OK | `exp − nbf = 600 s` en vivo; el **rechazo** de un token vencido, en `AuthenticationTests.An_expired_token_is_rejected` (no se esperan 10 minutos reales) |
| 19 | 401/403/404/409/422 siempre `problem+json` con `traceId`, sin detalles internos | OK | En vivo |
| 20 | Cerrar sesión → 204 y el mismo token da 401; los demás tokens siguen | OK | En vivo |

## Hallazgos de esta validación

1. **Defecto real corregido: aprobar o rechazar sin cuerpo JSON respondía 415.** El contrato dice que el cuerpo es opcional, pero un `POST` sin cuerpo (o con `Content-Type` de formulario y cuerpo vacío, lo que envían `curl` o PowerShell) fallaba con 415. Las pruebas automáticas no lo detectaban porque siempre enviaban JSON. Se corrigió leyendo el cuerpo opcional a mano (`ReadExpectedVersionAsync`) y se añadió la prueba `The_body_is_really_optional_...`. Nota: `Accepts<T>()` del enrutador no sirve para esto porque se aplica como restricción y devolvería el mismo 415; el cuerpo opcional se documenta en Swagger con `OptionalDecisionBodyFilter`.
2. **Defecto real corregido (detectado un paso antes por `SecurityPipelineTests`): los errores no llevaban `traceId`.** El contrato y la constitución lo prometen (correlation ID como `traceId`); se añadió `CustomizeProblemDetails` y una prueba que lo exige en 401, 403, 404 y 422.
3. Los fallos iniciales de los escenarios 1 y 4 eran del propio script de validación (conversión de fechas y de cuerpos `application/problem+json`, que PowerShell entrega como `byte[]`), no de la API.
