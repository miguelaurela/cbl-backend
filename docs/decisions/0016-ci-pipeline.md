# ADR-0016: CI pipeline with `.dacpac` deployment
- Status: Accepted
- Date: 2026-11-11

## Context
The schema lives in a SQL project (ADR-0005) and integration tests need a real SQL Server (ADR-0014, now ADR-0021). We want to build and ship through CI.

## Decision
Pipeline (GitHub Actions) with these steps: build the `.sqlproj` to a `.dacpac` → start SQL Server as a service container and deploy with `SqlPackage` → run unit and integration tests → on the main branch, generate `DeployReport` and publish the `.dacpac` as an artifact. Publishing release artifacts on tags is covered by [ADR-0024](0024-cd-publishes-artifacts-only.md).

## Alternatives considered
- CI without a real database (InMemory only).
- Manual schema deployment.

## Consequences
Slower CI, but it detects mismatches between the EF model and the schema. Deployment to a target environment is outside the scope of the test, except for publishing the artifact.

---

## Español

### ADR-0016: Pipeline de CI con despliegue del `.dacpac`
- Estado: Aceptada
- Fecha: 2026-11-11

#### Contexto
El esquema vive en un proyecto SQL (ADR-0005) y las pruebas de integración necesitan SQL Server real (ADR-0014, ahora ADR-0021). Se quiere compilar y enviar por CI.

#### Decisión
Pipeline (GitHub Actions) con estos pasos: compilar el `.sqlproj` a `.dacpac` → levantar SQL Server como contenedor de servicio y desplegar con `SqlPackage` → ejecutar pruebas unitarias y de integración → en la rama principal, generar `DeployReport` y publicar el `.dacpac` como artefacto. La publicación de artefactos de release en tags está cubierta por [ADR-0024](0024-cd-publishes-artifacts-only.md).

#### Alternativas consideradas
- CI sin base de datos real (solo InMemory).
- Despliegue manual del esquema.

#### Consecuencias
CI más lento, pero detecta desajustes entre el modelo de EF y el esquema. El despliegue a un entorno destino queda fuera del alcance de la prueba, salvo la publicación del artefacto.
