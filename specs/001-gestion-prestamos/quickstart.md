# Guía de validación (quickstart): gestión de préstamos bancarios – backend

**Rama**: `001-gestion-prestamos` | **Fecha**: 2026-11-11 | **Plan**: [plan.md](plan.md)

Guía para comprobar de extremo a extremo que la funcionalidad cumple la especificación. Es una guía de ejecución y validación: el código y las pruebas completas pertenecen a `tasks.md` y a la implementación. Contrato: [contracts/openapi.yaml](contracts/openapi.yaml). Datos: [data-model.md](data-model.md).

## Requisitos previos

| Herramienta | Uso | Nota |
|---|---|---|
| SDK de .NET 8 (8.0.x) | compilar y ejecutar | `global.json` lo exige; instalar con `winget install Microsoft.DotNet.SDK.8` |
| Git | control de versiones | ya instalado |
| `curl` o un cliente HTTP | llamar a la API | opcional, Swagger UI sirve en Development |
| SQL Server | solo para integración | **no** se necesita en local; lo provee CI |

No hace falta Docker: en local la API usa EF InMemory y las pruebas de integración se omiten.

## 1. Compilar y probar en local

```powershell
dotnet build
dotnet test --filter "Category!=Integration"
```

Resultado esperado: compilación sin advertencias (advertencias como error) y pruebas unitarias en verde. Un `dotnet test` sin filtro también termina en verde: las pruebas de integración aparecen como **omitidas**, no como fallidas, mientras no exista `CBL_TEST_SQL`.

## 2. Ejecutar la API en local (InMemory)

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:Database__Provider = "InMemory"
$env:Jwt__Key = "<clave de desarrollo de al menos 32 caracteres>"
dotnet run --project src/CBL.Api
```

La clave JWT solo se define por variable de entorno o user-secrets; nunca en el repositorio. En Development se publica Swagger UI en `/swagger`; fuera de Development `/swagger` responde 404.

## 3. Escenarios de validación

Cada escenario enlaza el requisito o criterio de éxito que comprueba. Rutas según el contrato.

| # | Escenario | Pasos | Resultado esperado | Trazabilidad |
|---|---|---|---|---|
| 1 | Login correcto | `POST /api/auth/login` con `usuario@test.com` / `123` | 200, `accessToken`, `expiresAt` ≈ ahora + 10 min, `user.role = Borrower` | FR-001, FR-002, FR-025 |
| 2 | Sin sesión | `GET /api/loans/mine` sin token | 401 `application/problem+json` | FR-001 |
| 3 | Solicitar préstamo válido | Borrower: `POST /api/loans` `{ "amount": 5000, "termMonths": 12 }` | 201, estado `Pending` | FR-006, FR-007 |
| 4 | Datos inválidos | Borrower: monto 0 o plazo 61 | 422 con `errors.amount` o `errors.termMonths` | FR-008–010, SC-007 |
| 5 | Admin no solicita | Admin: `POST /api/loans` | 403 | FR-006 |
| 6 | Aislamiento | Segundo Borrower pide el préstamo del primero | 404 | FR-011, SC-002 |
| 7 | Vista del admin | Admin: `GET /api/loans?status=Pending` | Lista con prestamista, monto y estado, sin paginar | FR-013 |
| 8 | Aprobar | Admin: `POST /api/loans/{id}/approve` | 200, estado `Approved`, `decidedById` y `decidedAt` completos | FR-014, FR-016, SC-005 |
| 9 | Estado fresco | Borrower: `GET /api/loans/{id}/status` antes y después de decidir | Tras la decisión muestra el estado nuevo | FR-018, FR-019, SC-003 |
| 10 | Decisión final | Admin: aprobar o rechazar de nuevo el mismo préstamo | 409 y el préstamo no cambia | FR-015 |
| 11 | Sin rol de admin | Borrower: `POST /api/loans/{id}/approve` | 403 | FR-014, FR-005 |
| 12 | Carrera (solo CI) | Dos decisiones simultáneas sobre un préstamo `Pending` | Una devuelve 200 y la otra 409; una sola fila de auditoría | FR-017, SC-004 |
| 13 | Límites | Admin: `PUT /api/parameters/loan-limits` con otros valores; Borrower solicita | La solicitud nueva se valida con los valores actualizados, sin redesplegar | FR-024, SC-010 |
| 14 | Usuarios | Admin: crear usuario con contraseña de 7 caracteres, luego de 8 | 422 y luego 201; correo duplicado → 409 | FR-020, FR-021 |
| 15 | Desactivar | Admin: `PUT /api/users/{id}` con `isActive=false` | El usuario ya no puede iniciar sesión; sus préstamos se conservan | FR-022 |
| 16 | Auto-baja | Admin se desactiva a sí mismo | 403 | FR-020, SC-011 |
| 17 | Bloqueo | 5 logins fallidos con el mismo correo (exista o no) y un sexto | 423 con `Retry-After`; mismo mensaje para correos existentes e inexistentes | FR-003, SC-008 |
| 18 | Expiración | Usar un token pasados 10 minutos | 401 | FR-002, SC-009 |
| 19 | Errores | Provocar 401/403/404/409/422 | Siempre `application/problem+json` con `traceId`, sin detalles internos | Constitución IV |
| 20 | Cerrar sesión | `POST /api/auth/logout` y luego reutilizar el mismo token en `GET /api/loans/mine` | 204 y después 401 aunque no hayan pasado 10 minutos | FR-004 |

Los escenarios 12 y la coincidencia EF ↔ esquema solo se ejecutan en CI.

## 4. Integración en CI

El flujo de `ci.yml` compila `db/CBL.Database` a `.dacpac`, levanta SQL Server como contenedor de servicio, publica el esquema con `SqlPackage`, define `CBL_TEST_SQL` y ejecuta todas las pruebas, incluidas las de integración. Para ejecutarlas en local con un SQL Server propio:

```powershell
$env:CBL_TEST_SQL = "<cadena de conexión a un SQL Server con el esquema ya publicado>"
dotnet test
```

## 5. Seguridad antes de subir

- Ningún secreto en el repositorio: `Jwt__Key`, cadenas de conexión y contraseñas fuera de `appsettings*.json`.
- Revisar `git diff --cached` de los archivos generados por Spec Kit y por los agentes antes de cada push.
- Las contraseñas `123` del seed son solo de ejemplo.

## 6. Entrega

Un tag `v*.*.*` dispara `release.yml`, que adjunta a un GitHub Release el `.dacpac`, el zip de publicación de la API y `SHA256SUMS.txt`. El `.dacpac` se aplica manualmente con `SqlPackage` y la API se ejecuta con `dotnet CBL.Api.dll`. No hay despliegue en la nube.
