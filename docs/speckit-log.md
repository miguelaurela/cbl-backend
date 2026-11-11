# Spec Kit Log – Backend / Bitácora de Spec Kit – Backend

One entry per phase. Fill in the date and "what I changed by hand" when each one is run. The prompts are in Spanish because the Spec Kit artifacts are written in Spanish (ADR-0022). Record here the pinned Spec Kit release tag and which agent ran each phase.

Una entrada por fase. Completa fecha y "qué cambié a mano" al ejecutar cada una. Los prompts están en español porque los artefactos de Spec Kit se redactan en español (ADR-0022). Registra aquí el tag de Spec Kit fijado y qué agente ejecutó cada fase.

## Phase 1 – /speckit.constitution / Fase 1 – /speckit.constitution
- Spec Kit version / Versión de Spec Kit: `specify-cli` 1.4.1.dev0 (commit `40dd00f`), instalado con `uv tool install --python 3.12`. Integraciones: Claude Code (predeterminada) y GitHub Copilot. / Integrations: Claude Code (default) and GitHub Copilot.
- Agent / Agente: Claude Code. En esta versión los comandos son skills con guion (`/speckit-constitution`), no con punto; el prompt se aplicó con el mismo contenido. / In this version commands are hyphenated skills (`/speckit-constitution`), not dotted; the prompt content was applied as is.
- Date / Fecha: 2026-11-11
- Prompt used / Prompt usado:

```
/speckit.constitution Redacta todos los artefactos en español.

Crea los principios del proyecto "CBL Project (Control Banking Loans)" (backend):

Arquitectura y código
- Arquitectura limpia/hexagonal con proyectos separados: CBL.Domain, CBL.Application, CBL.Infrastructure y CBL.Api. Domain no depende de ninguna otra capa ni de frameworks.
- Las reglas de negocio viven en el dominio. Los casos de uso viven en Application, organizados como comandos (RequestLoan, ApproveLoan, RejectLoan) y consultas (GetMyLoans, GetAllLoans, GetLoanStatus), sin MediatR ni buses.
- Los controladores solo traducen HTTP a casos de uso.
- .NET 8, ASP.NET Core Web API, EF Core. Todo el I/O es asíncrono (async/await). Código e identificadores en inglés.

Persistencia
- El esquema se define en un proyecto de base de datos SQL (.sqlproj, CBL.Database) que es la fuente de verdad. Se compila a .dacpac y se despliega por CI con SqlPackage.
- EF Core solo mapea el modelo a ese esquema mediante OnModelCreating (tipos, longitudes, índices, claves y token de versión explícitos). No se usan migraciones de EF.
- Base de datos en memoria de EF Core solo para ejecución local y pruebas unitarias.
- Control de concurrencia optimista con una columna de versión; los conflictos devuelven 409.
- La aprobación y su registro de auditoría se guardan de forma atómica.
- Los límites de monto se leen de una tabla de parámetros (SystemParameters) en base de datos y los gestiona el Admin.

Seguridad por defecto
- Autenticación JWT con vida de 10 minutos y sin refresh token, validando emisor, audiencia, firma y expiración. Autorización por roles (exactamente dos: Borrower y Admin) mediante policies.
- Todos los endpoints son privados salvo el login. El identificador del usuario sale siempre del token.
- Contraseñas con hash; secretos fuera del repositorio. Los usuarios creados por un Admin requieren una contraseña de al menos 8 caracteres (la contraseña semilla "123" es solo de ejemplo).
- Bloqueo de login tras 5 intentos fallidos durante 15 minutos (configurable).
- Pipeline de middleware: manejo global de errores, HTTPS/HSTS, cabeceras de seguridad, CORS restringido al frontend, rate limiting (más estricto en login), correlation ID y logging sin datos sensibles.
- DTOs de entrada y salida; nunca se exponen entidades. Consultas siempre parametrizadas.
- Swagger solo en desarrollo.

Consistencia y errores
- Una decisión final (aprobado o rechazado) es inmutable. Un Borrower solo puede editar o eliminar su propio préstamo mientras esté Pendiente; un Admin solo puede eliminar préstamos Pendientes; solo un Borrower puede solicitar préstamos.
- Todos los errores se devuelven como ProblemDetails (RFC 7807) con el código HTTP correcto (400, 401, 403, 404, 409, 422), sin detalles internos.

Rendimiento
- Caché sobre las consultas de estado de préstamos, solo en el lado de lectura, con expiración absoluta de 5 minutos e invalidación explícita después de confirmar el guardado de una decisión.

Calidad y pruebas
- Pruebas unitarias (xUnit) de dominio, validadores y casos de uso.
- Pruebas de integración con WebApplicationFactory contra SQL Server real, que corren únicamente en CI (contenedor de servicio de GitHub Actions con el esquema desplegado desde el .dacpac mediante SqlPackage; sin Testcontainers ni Docker local). En local se omiten salvo que se defina la variable de entorno CBL_TEST_SQL. Deben cubrir autenticación, autorización, aislamiento entre usuarios, decisiones, conflicto de concurrencia, caché, parámetros y formato de errores.
- Una prueba de integración verifica que el mapeo de EF coincide con el esquema del .sqlproj.
- Todo cambio funcional se describe primero en la especificación y se implementa con tareas trazables.
- Se prioriza un MVP funcional antes que el pulido, dado el tiempo limitado de la prueba.

Entrega
- El backend vive en su propio repositorio (cbl-backend). El contrato con el frontend (cbl-frontend) es la API REST documentada con OpenAPI.
- CI en GitHub Actions: compila, despliega el esquema en un SQL Server temporal, ejecuta todas las pruebas y publica el .dacpac como artefacto.
- CD solo publica artefactos: al subir un tag v*.*.* se adjuntan a un GitHub Release el .dacpac y el zip de publicación de la API. No hay despliegue en la nube.
```
- What it generated / Qué generó: `.specify/memory/constitution.md` v1.0.0 (ratificada 2026-11-11) con 7 principios (arquitectura limpia; persistencia con `.sqlproj`; seguridad por defecto; consistencia de negocio y errores; caché en lectura; calidad y pruebas; especificación primero y MVP), una sección de restricciones técnicas y de entrega, una de flujo de desarrollo y la gobernanza.
- What I changed by hand and why / Qué cambié a mano y por qué: nada en el contenido; solo se eliminó el informe de impacto temporal (comentario HTML) antes del commit, como indica la skill. Aprobada por el usuario. / Nothing in the content; only the temporary sync-impact HTML comment was removed before the commit, as the skill instructs. Approved by the user.

## Phase 2 – /speckit.specify / Fase 2 – /speckit.specify
- Agent / Agente: Claude Code (`/speckit-specify`). Rama `001-gestion-prestamos` creada a mano porque no hay extensión de git instalada (sin hook `before_specify`). / Branch `001-gestion-prestamos` created by hand because no git extension is installed (no `before_specify` hook).
- Date / Fecha: 2026-11-11
- Prompt used / Prompt usado:

```
/speckit.specify Redacta todos los artefactos en español.

CBL Project (Control Banking Loans): sistema de gestión de préstamos bancarios (simulación)

Un sistema donde los clientes solicitan préstamos y los administradores los aprueban o rechazan, con una API y una interfaz web.

Actores
- Prestamista (Borrower): cliente que solicita préstamos y consulta los suyos.
- Administrador (Admin): revisa todas las solicitudes, decide sobre ellas, gestiona usuarios y gestiona los límites de monto.

Historias de usuario
1. Como prestamista, quiero iniciar sesión con mi correo y contraseña para acceder a mis funciones según mi rol.
2. Como prestamista, quiero solicitar un préstamo indicando monto y plazo en meses para que sea evaluado.
3. Como prestamista, quiero ver la lista y el estado (pendiente, aprobado, rechazado) de mis préstamos.
4. Como administrador, quiero ver todas las solicitudes con el prestamista, monto y estado.
5. Como administrador, quiero aprobar o rechazar una solicitud pendiente.
6. Como administrador, quiero gestionar usuarios (crear, consultar, actualizar, desactivar) desde una pantalla de usuarios.
7. Como prestamista o administrador, quiero cerrar sesión de forma segura.
8. Como administrador, quiero consultar y modificar los límites de monto de los préstamos.

Reglas de negocio
- Estados posibles: Pendiente, Aprobado, Rechazado. Todo préstamo nuevo nace Pendiente.
- Roles: exactamente Admin y Borrower. Solo un Borrower puede solicitar préstamos; un Admin no puede solicitarlos.
- Monto: mayor que 0 y dentro de los límites mínimo y máximo definidos por un parámetro de configuración almacenado en base de datos (tabla de límites). Un administrador puede consultar y modificar esos límites, y cualquier cambio se aplica a las solicitudes nuevas sin redesplegar.
- Plazo: entero entre 1 y 60 meses.
- Un Borrower solo puede editar o eliminar su propio préstamo y únicamente mientras esté Pendiente. Un Admin solo puede eliminar préstamos Pendientes.
- Solo se puede decidir sobre préstamos Pendientes. Una decisión es final y no se puede revertir: los préstamos decididos son inmutables.
- Cada decisión registra quién la tomó y cuándo.
- Un prestamista nunca puede ver ni modificar préstamos de otros. Solo un administrador puede aprobar o rechazar.
- Si dos administradores deciden a la vez sobre el mismo préstamo, solo una decisión prevalece y la otra recibe un conflicto claro.
- Los usuarios creados por un administrador requieren una contraseña de al menos 8 caracteres.
- Los datos iniciales incluyen un prestamista (usuario@test.com / 123, contraseña solo de ejemplo), un administrador (admin@test.com / 123) y los valores por defecto del parámetro de límites (mínimo 1.000, máximo 100.000).

Criterios de aceptación clave
- Una solicitud con monto o plazo inválidos es rechazada con un mensaje claro por campo.
- Un usuario sin rol de administrador que intenta aprobar recibe un error de acceso denegado.
- Una solicitud sin sesión válida recibe un error de no autenticado.
- Tras aprobar o rechazar, el prestamista ve el nuevo estado sin información desactualizada.
- Consultar repetidamente el estado de los préstamos es rápido, y el resultado siempre refleja el último cambio.
- Si se modifica el parámetro de límites, las nuevas solicitudes se validan con los valores actualizados.
- Tras 5 intentos de inicio de sesión fallidos, el sistema bloquea temporalmente nuevos intentos (15 minutos, configurable) y responde con un mensaje claro, sin revelar si el correo existe.
- Una sesión expira a los 10 minutos, y el usuario debe iniciar sesión de nuevo.
- Cada decisión sobre un préstamo queda registrada con el administrador que la tomó y la fecha.

Interfaz (referencia: 3 pantallas principales, más gestión de usuarios del administrador)
- Login: correo y contraseña, con validaciones.
- Vista de prestamista: formulario para solicitar (monto y plazo) y lista "Mis préstamos" con estado.
- Vista de administrador: lista de solicitudes con acciones Aprobar y Rechazar en las pendientes, y gestión de límites de monto.
- Pantalla de usuarios del administrador: crear, consultar, actualizar y desactivar usuarios.
- Rutas de administrador protegidas para usuarios sin ese rol.

Fuera de alcance
- Cálculo de intereses, cuotas, desembolsos y pagos.
- Registro público de usuarios, recuperación de contraseña y notificaciones por correo.
```
- What it generated / Qué generó: `specs/001-gestion-prestamos/spec.md` (8 historias de usuario: 5 P1 y 3 P2; 30 requisitos funcionales; 10 criterios de éxito; casos límite, supuestos y fuera de alcance; 0 marcadores de aclaración), `specs/001-gestion-prestamos/checklists/requirements.md` (validado, 1 iteración) y `.specify/feature.json`. Encabezados traducidos al español; IDs `FR-###`/`SC-###` conservados. Supuestos añadidos por el agente para confirmar en la Fase 3: un admin no desactiva su propia cuenta; la desactivación surte efecto a más tardar al expirar la sesión; correo sin distinguir mayúsculas; los límites nuevos no afectan solicitudes ya registradas.
- What I changed by hand and why / Qué cambié a mano y por qué: nada; aprobada por el usuario tal como se generó. Antes de la aprobación el agente retiró "tabla de límites" del FR-023 por ser un detalle de implementación. / Nothing; approved by the user as generated. Before approval the agent removed "tabla de límites" from FR-023 as an implementation detail.

## Phase 3 – /speckit.clarify / Fase 3 – /speckit.clarify
- Agent / Agente: Claude Code (`/speckit-clarify`). Rama `001-gestion-prestamos`.
- Date / Fecha: 2026-11-11
- Questions it asked and my answers / Preguntas que hizo y mis respuestas: no existe en el repo una lista de "10 preguntas"; el agente hizo el barrido de ambigüedades y resolvió 8 puntos (límite de la skill: 5 preguntas al usuario). Quedaron registrados en `## Clarifications` de `spec.md`. / There is no list of "10 questions" in the repo; the agent scanned for ambiguities and resolved 8 points (skill cap: 5 questions to the user). Recorded in `## Clarifications` of `spec.md`.
  - Respondidas por el usuario (3, opción recomendada en las tres) / Answered by the user (3, recommended option each): (1) un admin NO puede desactivar su propia cuenta; (2) un usuario desactivado pierde el acceso al expirar su sesión (máx. 10 min); (3) el bloqueo de login se cuenta por correo, exista o no.
  - Resueltas desde ADR, sin preguntar (5) / Resolved from ADRs, not asked (5): límites nuevos solo afectan solicitudes nuevas (ADR-0008); desactivación lógica, nunca se elimina a usuarios con préstamos (ADR-0017); USD con formato `en-US`, interfaz en español, lista del admin con filtro por estado y sin paginación (ADR-0025). ADR-0023 ya estaba reflejado en la Fase 2.
  - Supuestos por defecto NO confirmados / Unconfirmed default assumptions: correo sin distinguir mayúsculas; al editar un préstamo Pendiente se cambian monto y plazo, revalidados con los límites vigentes.
- Spec changes / Cambios en el spec: FR-003, 013, 020, 022, 024 ajustados; FR-031 y SC-011 añadidos; escenarios y casos límite nuevos; 31 FR y 11 SC en total; checklist sin regresiones (15/15) y 0 marcadores. / FR-003, 013, 020, 022, 024 adjusted; FR-031 and SC-011 added; new scenarios and edge cases; 31 FR and 11 SC total; checklist with no regressions (15/15) and 0 markers.
- What I changed by hand and why / Qué cambié a mano y por qué: nada; aprobada por el usuario tal como se generó. / Nothing; approved by the user as generated.

## Phase 4 – /speckit.plan / Fase 4 – /speckit.plan
- Agent / Agente: Claude Code (`/speckit-plan`). Rama `001-gestion-prestamos`.
- Date / Fecha: 2026-11-11
- Prompt used / Prompt usado: (paste; it pins .NET 8, EF Core + InMemory, `.sqlproj`, FluentValidation, xUnit, `WebApplicationFactory`, SQL Server service container in CI with `SqlPackage`, no Testcontainers, CI and release workflows / pegar; aquí se fija .NET 8, EF Core + InMemory, `.sqlproj`, FluentValidation, xUnit, `WebApplicationFactory`, contenedor de servicio de SQL Server en CI con `SqlPackage`, sin Testcontainers, workflows de CI y release)
- Prompt applied / Prompt aplicado (resumen): "Fija el stack: .NET 8, EF Core + InMemory, `.sqlproj`, FluentValidation, xUnit y `WebApplicationFactory`; SQL Server como contenedor de servicio en CI con `SqlPackage`, sin Testcontainers; workflows de CI y de release. Usa `docs/planning/tasks-backend.md` como borrador." (Se aplicó el contenido del prompt descrito arriba; no había un texto literal en el log.) / (The content described above was applied; there was no literal text in the log.)
- What it generated / Qué generó: `specs/001-gestion-prestamos/plan.md` (contexto técnico, verificación de la constitución sin violaciones, estructura de código, tabla de desviaciones D1–D8), `research.md` (16 decisiones), `data-model.md` (4 tablas, transiciones, validaciones), `contracts/openapi.yaml` (18 operaciones, YAML válido, referencias resueltas) y `quickstart.md` (20 escenarios).
- Deviations from `docs/planning/tasks-backend.md` / Desviaciones del borrador: nombres `CBL.*`; `GET /api/loans` sin paginación; se omiten `GET /api/auth/me`, `GET /api/loans/{id}/audit` y `DELETE /api/users/{id}` (fuera del spec); bloqueo de login por correo en memoria; auto-baja de admin → 403; cuerpo opcional `version` al decidir; **nuevo `POST /api/auth/logout`** con revocación de `jti` en memoria (FR-004; no está en el contrato asumido por el frontend). / `CBL.*` names; no paging; endpoints outside the spec dropped; in-memory lockout per email; admin self-deactivation → 403; optional `version` on decisions; **new `POST /api/auth/logout`** with in-memory `jti` revocation (FR-004; not in the contract the frontend assumed).
- Pending for the frontend / Pendiente para el frontend: llamar a `POST /api/auth/logout` al cerrar sesión y conciliar con este `openapi.yaml` (FE-74). / Call `POST /api/auth/logout` on sign-out and reconcile with this `openapi.yaml` (FE-74).
- Environment note / Nota de entorno: la máquina solo tiene SDK de .NET 6 y 9; `global.json` exigirá el 8 antes de implementar. / The machine only has .NET SDK 6 and 9; `global.json` will require 8 before implementation.
- What I changed by hand and why / Qué cambié a mano y por qué: nada; aprobada por el usuario tal como se generó, incluido el endpoint de logout. / Nothing; approved by the user as generated, including the logout endpoint.

## Phase 5 – /speckit.tasks / Fase 5 – /speckit.tasks
- Date / Fecha:
- What I changed by hand and why / Qué cambié a mano y por qué:

## Phase 6 – /speckit.analyze / Fase 6 – /speckit.analyze
- Date / Fecha:
- Result (pasted) / Resultado (pegado):

## Phase 7 – /speckit.implement / Fase 7 – /speckit.implement
- Date / Fecha:
- Notes / Notas:
