# Checklist de calidad de la especificación: Gestión de préstamos bancarios

**Propósito**: validar que la especificación esté completa y con buena calidad antes de pasar a la planificación
**Creado**: 2026-11-11
**Funcionalidad**: [spec.md](../spec.md)

## Calidad del contenido

- [x] Sin detalles de implementación (lenguajes, frameworks, APIs)
- [x] Centrada en el valor para el usuario y las necesidades del negocio
- [x] Redactada para interesados no técnicos
- [x] Todas las secciones obligatorias completas

## Completitud de los requisitos

- [x] No quedan marcadores [NEEDS CLARIFICATION]
- [x] Los requisitos son comprobables y no ambiguos
- [x] Los criterios de éxito son medibles
- [x] Los criterios de éxito no dependen de la tecnología
- [x] Todos los escenarios de aceptación están definidos
- [x] Los casos límite están identificados
- [x] El alcance está claramente acotado
- [x] Las dependencias y los supuestos están identificados

## Preparación de la funcionalidad

- [x] Todos los requisitos funcionales tienen criterios de aceptación claros
- [x] Los escenarios de usuario cubren los flujos principales
- [x] La funcionalidad cumple los resultados medibles de los criterios de éxito
- [x] No se filtran detalles de implementación en la especificación

## Notas

- Validación en 1 iteración: se retiró la mención a la "tabla de límites" del FR-023 por ser un detalle de implementación (el diseño de datos pertenece a `/speckit-plan`).
- Se mantienen términos que vienen del enunciado del usuario y forman parte del contrato de negocio: "API REST" (contrato con el frontend) y los roles `Admin` y `Borrower`.
- Supuestos añadidos que NO estaban en el prompt y conviene confirmar en `/speckit-clarify`: (1) un administrador no puede desactivar su propia cuenta; (2) la desactivación de un usuario con sesión vigente surte efecto a más tardar al expirar la sesión; (3) el correo se compara sin distinguir mayúsculas y minúsculas; (4) los límites nuevos no afectan a solicitudes ya registradas.
