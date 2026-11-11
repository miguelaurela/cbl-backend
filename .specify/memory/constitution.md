# Constitución de CBL Project (Control Banking Loans) – Backend

Sistema de gestión de préstamos bancarios (simulación): los prestamistas solicitan préstamos y los
administradores los aprueban o rechazan. Este documento rige el repositorio `cbl-backend`. Los
artefactos de Spec Kit se redactan en español; el código y los identificadores, en inglés.

## Principios fundamentales

### I. Arquitectura limpia con capas separadas

- El backend se organiza en cuatro proyectos: `CBL.Domain`, `CBL.Application`,
  `CBL.Infrastructure` y `CBL.Api`. `CBL.Domain` MUST NOT depender de ninguna otra capa ni de
  frameworks.
- Las reglas de negocio MUST vivir en el dominio.
- Los casos de uso MUST vivir en `CBL.Application`, organizados como comandos (`RequestLoan`,
  `ApproveLoan`, `RejectLoan`) y consultas (`GetMyLoans`, `GetAllLoans`, `GetLoanStatus`). No se
  usan MediatR ni buses de mensajes.
- Los controladores MUST limitarse a traducir HTTP a casos de uso.
- La plataforma es .NET 8 con ASP.NET Core Web API y EF Core. Todo el I/O MUST ser asíncrono
  (`async`/`await`).

**Razón**: aislar las reglas de negocio las hace probables sin infraestructura y mantiene el
alcance proporcionado a una prueba técnica.

### II. Persistencia: el `.sqlproj` es la fuente de verdad

- El esquema MUST definirse en un proyecto de base de datos SQL (`CBL.Database`, `.sqlproj`),
  compilarse a `.dacpac` y desplegarse en CI con SqlPackage.
- EF Core SOLO mapea el modelo a ese esquema mediante `OnModelCreating`, con tipos, longitudes,
  índices, claves y token de versión explícitos. NO se usan migraciones de EF.
- La base de datos en memoria de EF Core MUST usarse solo para ejecución local y pruebas
  unitarias.
- El control de concurrencia MUST ser optimista con una columna de versión; los conflictos
  devuelven 409.
- La aprobación y su registro de auditoría MUST guardarse de forma atómica.
- Los límites de monto MUST leerse de la tabla `SystemParameters` y los gestiona el Admin.

**Razón**: una sola fuente de verdad del esquema evita divergencias entre el modelo de EF y la
base de datos real.

### III. Seguridad por defecto (NO NEGOCIABLE)

- La autenticación MUST ser JWT con vida de 10 minutos y sin refresh token, validando emisor,
  audiencia, firma y expiración.
- La autorización MUST basarse en roles mediante policies, con exactamente dos roles: `Borrower`
  y `Admin`.
- Todos los endpoints MUST ser privados salvo el login. El identificador del usuario MUST salir
  siempre del token.
- Las contraseñas MUST almacenarse con hash y los secretos MUST mantenerse fuera del repositorio
  (variables de entorno o user-secrets). Los usuarios creados por un Admin MUST tener una
  contraseña de al menos 8 caracteres; la contraseña semilla `123` es solo de ejemplo.
- El login MUST bloquearse tras 5 intentos fallidos durante 15 minutos (configurable), con un
  mensaje que no revele si el correo existe.
- El pipeline de middleware MUST incluir, en este orden lógico: manejo global de errores,
  HTTPS/HSTS, cabeceras de seguridad, CORS restringido al frontend, rate limiting (más estricto en
  login), correlation ID y logging sin datos sensibles.
- La API MUST usar DTOs de entrada y salida; nunca se exponen entidades. Las consultas MUST ser
  siempre parametrizadas.
- Swagger MUST estar habilitado solo en desarrollo.

**Razón**: es un sistema financiero simulado; la seguridad no se pospone ni se activa a posteriori.

### IV. Consistencia de negocio y errores explícitos

- Una decisión final (aprobado o rechazado) es inmutable y solo se decide sobre préstamos
  Pendientes.
- Un Borrower MUST poder editar o eliminar únicamente su propio préstamo mientras esté Pendiente;
  un Admin MUST poder eliminar únicamente préstamos Pendientes; solo un Borrower puede solicitar
  préstamos.
- Todos los errores MUST devolverse como `ProblemDetails` (RFC 7807) con el código HTTP correcto
  (400, 401, 403, 404, 409, 422), sin detalles internos.

**Razón**: reglas de transición claras y errores predecibles que el frontend pueda interpretar.

### V. Caché en el lado de lectura

- La caché MUST aplicarse solo a las consultas de estado de préstamos, con expiración absoluta de
  5 minutos.
- La invalidación MUST ser explícita y ocurrir después de confirmar el guardado de una decisión.

**Razón**: acelerar lecturas repetidas sin servir nunca un estado desactualizado tras una
decisión.

### VI. Calidad y pruebas

- Las pruebas unitarias (xUnit) MUST cubrir dominio, validadores y casos de uso.
- Las pruebas de integración MUST usar `WebApplicationFactory` contra SQL Server real y correr
  únicamente en CI (contenedor de servicio de GitHub Actions con el esquema desplegado desde el
  `.dacpac` mediante SqlPackage; sin Testcontainers ni Docker local). En local se omiten salvo
  que se defina la variable de entorno `CBL_TEST_SQL`.
- Las pruebas de integración MUST cubrir autenticación, autorización, aislamiento entre usuarios,
  decisiones, conflicto de concurrencia, caché, parámetros y formato de errores.
- Una prueba de integración MUST verificar que el mapeo de EF coincide con el esquema del
  `.sqlproj`.

**Razón**: la atomicidad y la concurrencia no las garantiza la base de datos en memoria; solo un
SQL Server real las valida.

### VII. Especificación primero y MVP antes que pulido

- Todo cambio funcional MUST describirse primero en la especificación y MUST implementarse con
  tareas trazables.
- Se MUST priorizar un MVP funcional antes que el pulido, dado el tiempo limitado de la prueba.

**Razón**: la trazabilidad especificación → plan → tareas → código es el propósito del uso de Spec
Kit, y el alcance es acotado.

## Restricciones técnicas y de entrega

- El backend vive en su propio repositorio (`cbl-backend`). El contrato con el frontend
  (`cbl-frontend`) es la API REST documentada con OpenAPI.
- CI en GitHub Actions: compila, despliega el esquema en un SQL Server temporal, ejecuta todas las
  pruebas y publica el `.dacpac` como artefacto.
- CD solo publica artefactos: al subir un tag `v*.*.*` se adjuntan a un GitHub Release el
  `.dacpac` y el zip de publicación de la API. NO hay despliegue en la nube.
- Fuera de alcance: cálculo de intereses, cuotas, desembolsos y pagos.

## Flujo de desarrollo y puertas de calidad

- El trabajo avanza por las fases de Spec Kit (constitución, especificación, aclaración, plan,
  tareas, análisis, implementación) y cada fase se registra en `docs/speckit-log.md`.
- Una pull request o fusión MUST verificar el cumplimiento de esta constitución. Cualquier
  complejidad adicional MUST justificarse por escrito.
- Las decisiones arquitectónicas se registran como ADR en `docs/decisions/`; una decisión
  cambiada se documenta en un ADR nuevo que reemplaza al anterior, sin editarlo.

## Gobernanza

Esta constitución prevalece sobre cualquier otra práctica del repositorio. Una enmienda MUST
documentarse con su justificación, aprobarse por el responsable del proyecto y, si altera un
principio, incluir un plan de migración del código afectado. El versionado sigue SemVer: MAJOR
para eliminaciones o redefiniciones incompatibles de principios, MINOR para principios o secciones
nuevas o ampliaciones materiales, PATCH para aclaraciones y correcciones de redacción. El
cumplimiento se revisa en cada fase de Spec Kit (especialmente `analyze`) y en cada pull request.

**Versión**: 1.0.0 | **Ratificada**: 2026-11-11 | **Última enmienda**: 2026-11-11
