🇬🇧 [English](00-analysis.md)

# Análisis de la prueba técnica Makers (adaptada a .NET + Angular) – CBL Project (Control Banking Loans)

## 1. Alcance
- **Teórica (30%)**: 6 preguntas abiertas, respondidas por escrito y justificadas.
- **Práctica (70%)**: API REST de préstamos + frontend Angular. Código en repositorio público de GitHub. Estimado: 2 h práctica, 60 min teoría.
- Adaptación: el PDF está escrito para Java/Spring Boot; aquí se resuelve en **.NET** (backend) y **Angular** (frontend), en **dos repositorios** (`cbl-backend`, `cbl-frontend`).

## 2. Traducción del stack
| Requisito del PDF | Equivalente en .NET |
|---|---|
| Spring Boot REST | ASP.NET Core Web API (.NET 8 LTS) |
| Spring Data JPA / Hibernate | Entity Framework Core |
| Hibernate Validator | FluentValidation |
| Spring Security | JWT Bearer + policies y roles |
| Spring WebFlux (donde amerite) | `async/await` de extremo a extremo |
| Arquitectura limpia/hexagonal | Domain, Application, Infrastructure, Api |
| EHCache | `IMemoryCache` detrás de una interfaz |
| `@Transactional` | Unit of Work con `SaveChanges` atómico |
| JUnit 5 + Spring Boot Test | xUnit + Moq/NSubstitute + FluentAssertions + `WebApplicationFactory` |
| H2 / PostgreSQL / MySQL | EF en memoria (local/unitarias) y SQL Server real (solo en CI) |
| Errores HTTP claros | `ProblemDetails` (RFC 7807) con `IExceptionHandler` |

## 3. Requisitos funcionales
- RF-01 Solicitar préstamo (monto y plazo en meses).
- RF-02 Admin aprueba o rechaza.
- RF-03 El Prestamista (`Borrower`) consulta el estado de sus préstamos.
- RF-04 CRUD de préstamos y usuarios respetando reglas de negocio.
- RF-05 Login con JWT, roles `Borrower` (Prestamista) y `Admin`.
- RF-06 Frontend: enviar solicitudes, consultar estado; admin ve y cambia estados.

Credenciales de ejemplo del PDF (datos semilla): `usuario@test.com` / `123` y `admin@test.com` / `123`.

## 4. Requisitos no funcionales
RNF-01 Arquitectura limpia · RNF-02 Caché de estado con invalidación · RNF-03 Autorización por rol · RNF-04 Aprobación transaccional y concurrencia · RNF-05 Pruebas unitarias · RNF-06 Errores HTTP claros · RNF-07 Reactive Forms · RNF-08 Guards · RNF-09 Estado seguro (NgRx) · RNF-10 UI cercana al mockup.

## 5. Ambigüedades y decisiones tomadas
| # | Tema | Decisión |
|---|---|---|
| 1 | Plazo | Se incluye, en meses (el mockup solo muestra monto) |
| 2 | Reglas de negocio | Plazo 1–60 meses; solo se decide sobre `Pendiente`; decisión final inmutable; **límites de monto en parámetro de base de datos** (por defecto 1.000–100.000) |
| 3 | Estados | `Pendiente`, `Aprobado`, `Rechazado` |
| 4 | CRUD de usuarios | Lo gestiona el admin; hay seed; sin auto-registro |
| 5 | Qué hace "Aprobar" | Cambia estado, registra quién y cuándo, deja auditoría, todo atómico |
| 6 | Intereses y cuotas | Fuera de alcance, diseño extensible |
| 7 | Pregunta teórica 6 (Spring) | Se responde con el equivalente ASP.NET Core y mención breve a Spring |
| 8 | Estado en Angular | NgRx |
| 9 | Base de datos | `.sqlproj` como fuente de verdad, sin migraciones de EF; EF en memoria para local/unitarias; SQL Server real solo en CI |
| 10 | Repositorios | Dos: backend y frontend |
| 11 | CQRS | Ligero, por convención, sin MediatR |
| 12 | Token en frontend | Solo en memoria, vida corta (10 minutos), sin refresh token |

## 6. Condición de carrera
Único punto de riesgo real: dos admins deciden sobre el mismo préstamo. Se resuelve con transición validada en el dominio, columna de versión (concurrencia optimista) y respuesta 409. La invalidación de caché ocurre después de confirmar el guardado. La prueba de carrera se ejecuta contra SQL Server real, solo en CI.

## 7. Seguridad (pipeline de middleware)
1. Manejo global de excepciones → `ProblemDetails`
2. HTTPS redirection + HSTS
3. Cabeceras de seguridad
4. CORS restringido al origen del frontend
5. Rate limiting (más estricto en `/login`)
6. Correlation ID + logging sin datos sensibles
7. Authentication (JWT Bearer)
8. Authorization (policies, `AdminOnly`)

Adicionales: el id del usuario sale siempre del token (anti-IDOR), DTOs de entrada/salida, consultas parametrizadas, mensaje de login genérico y bloqueo temporal, secretos fuera del repo, Swagger solo en desarrollo, límite de tamaño de cuerpo.

## 8. Pruebas de integración (mínimo)
Se ejecutan solo en CI (contenedor de servicio de SQL Server); en local se omiten salvo que se defina `CBL_TEST_SQL` (ADR-0021).
1. Sin token → 401; no admin aprobando → 403.
2. Solicitud válida → 201; inválida → 400/422 con errores por campo.
3. Un prestamista no ve préstamos de otro.
4. Aprobar/rechazar cambia estado y deja auditoría.
5. Decidir dos veces → 409.
6. Carrera: dos decisiones simultáneas, solo una prevalece.
7. Caché actualizada tras decidir.
8. Cambiar el parámetro de límites afecta nuevas solicitudes.
9. Errores en formato `ProblemDetails`.
10. Rate limiting en login.
11. El mapeo de EF coincide con el esquema del `.sqlproj`.

## 9. Estructura propuesta
```
/src   CBL.Domain · CBL.Application · CBL.Infrastructure (DbContext + OnModelCreating) · CBL.Api
/tests CBL.UnitTests · CBL.IntegrationTests
/db    CBL.Database (Microsoft.Build.Sql): Tables/ (Users, Loans, LoanDecisionAudit, SystemParameters), PostDeployment/Seed.sql
/.github/workflows
```
Flujo de CI (GitHub Actions): compilar `.sqlproj` → `.dacpac`; levantar SQL Server como contenedor de servicio y desplegar con `SqlPackage`; ejecutar pruebas unitarias e integración; en la rama principal, publicar el `.dacpac` (con `DeployReport` previo). CD: al subir un tag `v*.*.*` se publican los artefactos en un GitHub Release (ADR-0024).

## 10. Riesgos
- Con EF en memoria la atomicidad no la garantiza el motor: se compensa con pruebas en SQL Server real (en CI).
- El alcance es amplio para 2 h: priorizar MVP (login y roles → solicitar → aprobar/rechazar → listar → caché y pruebas).
