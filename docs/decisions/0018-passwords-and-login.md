# ADR-0018: Hashed passwords and login protection
- Status: Accepted
- Date: 2026-11-11

## Context
The sample credentials are simple (`123`) and login is the endpoint most exposed to brute force.

## Decision
PBKDF2 password hashing (`PasswordHasher`); the seed passwords are hashed too, with the hash already computed in the post-deployment script. Generic login message that does not reveal whether the email exists, safe hash comparison, strict rate limiting on `/login` and temporary lockout after repeated failed attempts (values in ADR-0009).

## Alternatives considered
- Plain-text passwords in the seed (discarded).
- Bcrypt/Argon2 (valid, but PBKDF2 already ships with the framework).

## Consequences
The sample passwords are for the test only and are documented as such.

---

## Español

### ADR-0018: Contraseñas con hash y protección del login
- Estado: Aceptada
- Fecha: 2026-11-11

#### Contexto
Las credenciales de ejemplo son simples (`123`) y el login es el punto más expuesto a fuerza bruta.

#### Decisión
Contraseñas con hash PBKDF2 (`PasswordHasher`); las del seed también van hasheadas, con el hash ya calculado en el script de post-despliegue. Mensaje de login genérico que no revela si el correo existe, comparación segura del hash, rate limiting estricto en `/login` y bloqueo temporal tras varios intentos fallidos (valores en ADR-0009).

#### Alternativas consideradas
- Contraseñas en texto plano en el seed (descartada).
- Bcrypt/Argon2 (válidos, pero PBKDF2 ya viene con el framework).

#### Consecuencias
Las contraseñas de ejemplo son solo para la prueba y se documentan como tales.
