# ADR-0024: CD publishes artifacts only
- Status: Accepted
- Date: 2026-11-11

## Context
The test has no target environment, and no cloud account or secrets are available. Delivery still benefits from reproducible, downloadable build outputs.

## Decision
Continuous delivery means **publishing artifacts**, nothing more. A tag matching `v*.*.*` triggers a release workflow that rebuilds, runs unit tests and creates a GitHub Release with the `.dacpac`, the API publish zip and a `SHA256SUMS.txt` (the frontend repo attaches its own web zip). Only `GITHUB_TOKEN` is used. There is no cloud deployment. See ADR-0016 for CI.

## Alternatives considered
- Deploy to a cloud environment (needs accounts, secrets and cost).
- Publish artifacts on every push to `main` (noisy, unversioned).

## Consequences
The `.dacpac` is applied manually with `SqlPackage`, the API zip runs with `dotnet CBL.Api.dll` and the web zip is static files. The README documents this.

---

## Español

### ADR-0024: El CD solo publica artefactos
- Estado: Aceptada
- Fecha: 2026-11-11

#### Contexto
La prueba no tiene entorno destino ni hay cuenta en la nube ni secretos. Aun así, la entrega se beneficia de salidas de compilación reproducibles y descargables.

#### Decisión
La entrega continua significa **publicar artefactos**, nada más. Un tag con formato `v*.*.*` dispara un workflow de release que recompila, ejecuta las pruebas unitarias y crea un GitHub Release con el `.dacpac`, el zip de publicación de la API y un `SHA256SUMS.txt` (el repo de frontend adjunta su propio zip web). Solo se usa `GITHUB_TOKEN`. No hay despliegue en la nube. Ver ADR-0016 para CI.

#### Alternativas consideradas
- Desplegar en un entorno en la nube (requiere cuentas, secretos y coste).
- Publicar artefactos en cada push a `main` (ruidoso, sin versión).

#### Consecuencias
El `.dacpac` se aplica manualmente con `SqlPackage`, el zip de la API se ejecuta con `dotnet CBL.Api.dll` y el zip web son archivos estáticos. El README lo documenta.
