# Especificación de funcionalidad: CBL Project (Control Banking Loans) – Gestión de préstamos bancarios

**Rama de la funcionalidad**: `001-gestion-prestamos`

**Creada**: 2026-11-11

**Estado**: Borrador

**Entrada**: Descripción del usuario: "CBL Project (Control Banking Loans): sistema de gestión de préstamos bancarios (simulación). Un sistema donde los clientes solicitan préstamos y los administradores los aprueban o rechazan, con una API y una interfaz web."

## Actores

- **Prestamista (Borrower)**: cliente que solicita préstamos y consulta los suyos.
- **Administrador (Admin)**: revisa todas las solicitudes, decide sobre ellas, gestiona usuarios y gestiona los límites de monto.

Existen exactamente estos dos roles. Todo usuario tiene uno y solo uno.

## Escenarios de usuario y pruebas *(obligatorio)*

### Historia de usuario 1 – Iniciar sesión (Prioridad: P1)

Como prestamista o administrador, quiero iniciar sesión con mi correo y contraseña para acceder a las funciones que corresponden a mi rol.

**Por qué esta prioridad**: sin identificación no hay ninguna otra función; todo el sistema es privado salvo el inicio de sesión.

**Prueba independiente**: iniciar sesión con las credenciales semilla de cada rol y comprobar que solo se accede a las funciones de ese rol.

**Escenarios de aceptación**:

1. **Dado** un usuario activo con credenciales válidas, **cuando** inicia sesión, **entonces** obtiene acceso a las funciones de su rol.
2. **Dado** credenciales incorrectas, **cuando** intenta iniciar sesión, **entonces** recibe un mensaje genérico que no revela si el correo existe.
3. **Dado** un usuario con 5 intentos de inicio de sesión fallidos, **cuando** intenta de nuevo dentro de los 15 minutos siguientes, **entonces** el sistema bloquea el intento y responde con un mensaje claro, sin revelar si el correo existe.
4. **Dado** una sesión iniciada hace 10 minutos, **cuando** el usuario intenta hacer cualquier operación, **entonces** el sistema lo trata como no autenticado y debe iniciar sesión de nuevo.
5. **Dado** una solicitud sin sesión válida a cualquier función distinta del inicio de sesión, **cuando** se procesa, **entonces** recibe un error de no autenticado.

---

### Historia de usuario 2 – Solicitar un préstamo (Prioridad: P1)

Como prestamista, quiero solicitar un préstamo indicando monto y plazo en meses para que sea evaluado.

**Por qué esta prioridad**: es la función central del negocio; sin solicitudes no hay nada que evaluar.

**Prueba independiente**: con una sesión de prestamista, enviar una solicitud válida y comprobar que queda registrada como Pendiente; enviar una inválida y comprobar el mensaje por campo.

**Escenarios de aceptación**:

1. **Dado** un prestamista con sesión válida, **cuando** solicita un préstamo con monto dentro de los límites vigentes y plazo entre 1 y 60 meses, **entonces** el préstamo queda registrado con estado Pendiente.
2. **Dado** un monto menor o igual que 0, o fuera de los límites mínimo y máximo vigentes, **cuando** se envía la solicitud, **entonces** se rechaza con un mensaje claro para el campo monto.
3. **Dado** un plazo que no es un entero entre 1 y 60, **cuando** se envía la solicitud, **entonces** se rechaza con un mensaje claro para el campo plazo.
4. **Dado** un administrador con sesión válida, **cuando** intenta solicitar un préstamo, **entonces** se le deniega el acceso.

---

### Historia de usuario 3 – Consultar mis préstamos y su estado (Prioridad: P1)

Como prestamista, quiero ver la lista y el estado (Pendiente, Aprobado, Rechazado) de mis préstamos.

**Por qué esta prioridad**: cierra el ciclo para el cliente: solicitar y saber el resultado.

**Prueba independiente**: con dos prestamistas que tienen préstamos, comprobar que cada uno ve solo los suyos con su estado actual.

**Escenarios de aceptación**:

1. **Dado** un prestamista con préstamos, **cuando** consulta su lista, **entonces** ve únicamente sus propios préstamos, cada uno con su estado.
2. **Dado** un préstamo que un administrador acaba de aprobar o rechazar, **cuando** su prestamista lo consulta, **entonces** ve el estado nuevo, sin información desactualizada.
3. **Dado** un prestamista que intenta ver o modificar un préstamo de otro prestamista, **cuando** lo intenta, **entonces** no puede acceder a él.
4. **Dado** un prestamista con sus préstamos Pendientes, **cuando** edita o elimina uno propio mientras sigue Pendiente, **entonces** el cambio se aplica; **y cuando** intenta editar o eliminar uno ya decidido, **entonces** se rechaza la operación.

---

### Historia de usuario 4 – Ver todas las solicitudes (Prioridad: P1)

Como administrador, quiero ver todas las solicitudes con el prestamista, el monto y el estado.

**Por qué esta prioridad**: es la base para que el administrador pueda decidir.

**Prueba independiente**: con préstamos de varios prestamistas, comprobar que el administrador los ve todos con prestamista, monto y estado, y que un prestamista no puede acceder a esa vista.

**Escenarios de aceptación**:

1. **Dado** un administrador con sesión válida, **cuando** consulta las solicitudes, **entonces** ve todos los préstamos con prestamista, monto y estado.
2. **Dado** un prestamista con sesión válida, **cuando** intenta acceder a la vista de todas las solicitudes, **entonces** se le deniega el acceso.

---

### Historia de usuario 5 – Aprobar o rechazar una solicitud (Prioridad: P1)

Como administrador, quiero aprobar o rechazar una solicitud pendiente.

**Por qué esta prioridad**: es la otra mitad del flujo central del negocio y el punto donde se exige mayor consistencia.

**Prueba independiente**: decidir sobre un préstamo Pendiente y comprobar el nuevo estado, el registro de quién y cuándo, y que no se puede decidir de nuevo.

**Escenarios de aceptación**:

1. **Dado** un préstamo Pendiente, **cuando** un administrador lo aprueba o lo rechaza, **entonces** el estado cambia y queda registrado quién decidió y cuándo.
2. **Dado** un préstamo ya Aprobado o Rechazado, **cuando** un administrador intenta decidir de nuevo, **entonces** se rechaza la operación y el préstamo no cambia.
3. **Dado** un usuario sin rol de administrador, **cuando** intenta aprobar o rechazar, **entonces** recibe un error de acceso denegado.
4. **Dado** que dos administradores deciden a la vez sobre el mismo préstamo Pendiente, **cuando** ambas decisiones llegan, **entonces** solo una prevalece y la otra recibe un conflicto claro.
5. **Dado** un administrador, **cuando** intenta eliminar un préstamo, **entonces** solo puede hacerlo si está Pendiente.

---

### Historia de usuario 6 – Gestionar usuarios (Prioridad: P2)

Como administrador, quiero gestionar usuarios (crear, consultar, actualizar, desactivar) desde una pantalla de usuarios.

**Por qué esta prioridad**: necesaria para operar el sistema sin registro público, pero el flujo central funciona con los usuarios semilla.

**Prueba independiente**: crear un usuario con contraseña válida, consultarlo, actualizarlo y desactivarlo, y comprobar que el usuario desactivado ya no puede iniciar sesión.

**Escenarios de aceptación**:

1. **Dado** un administrador, **cuando** crea un usuario con correo único, rol válido y contraseña de al menos 8 caracteres, **entonces** el usuario queda creado y puede iniciar sesión.
2. **Dado** una contraseña de menos de 8 caracteres o un correo ya existente, **cuando** el administrador intenta crear el usuario, **entonces** se rechaza con un mensaje claro por campo.
3. **Dado** un usuario desactivado, **cuando** intenta iniciar sesión, **entonces** no obtiene acceso.
4. **Dado** un prestamista, **cuando** intenta acceder a la gestión de usuarios, **entonces** se le deniega el acceso.

---

### Historia de usuario 7 – Gestionar los límites de monto (Prioridad: P2)

Como administrador, quiero consultar y modificar los límites de monto de los préstamos.

**Por qué esta prioridad**: da flexibilidad al negocio sin redesplegar, pero el flujo central funciona con los valores por defecto.

**Prueba independiente**: modificar los límites y comprobar que una solicitud nueva se valida con los valores actualizados.

**Escenarios de aceptación**:

1. **Dado** un administrador, **cuando** consulta los límites, **entonces** ve el mínimo y el máximo vigentes.
2. **Dado** un administrador que modifica los límites con valores válidos, **cuando** un prestamista solicita después un préstamo, **entonces** la solicitud se valida con los valores actualizados, sin redesplegar el sistema.
3. **Dado** un prestamista, **cuando** intenta modificar los límites, **entonces** se le deniega el acceso.

---

### Historia de usuario 8 – Cerrar sesión (Prioridad: P2)

Como prestamista o administrador, quiero cerrar sesión de forma segura.

**Por qué esta prioridad**: complementa el acceso seguro, pero la vigencia de 10 minutos ya acota el riesgo.

**Prueba independiente**: cerrar sesión y comprobar que no se puede volver a las funciones privadas sin iniciar sesión de nuevo.

**Escenarios de aceptación**:

1. **Dado** un usuario con sesión iniciada, **cuando** cierra sesión, **entonces** ya no puede usar funciones privadas hasta iniciar sesión de nuevo.

---

### Casos límite

- Monto exactamente igual al mínimo o al máximo vigente: se acepta.
- Plazo de 0 o 61 meses, decimal o no numérico: se rechaza con mensaje por campo.
- Dos administradores deciden simultáneamente sobre el mismo préstamo: solo una decisión prevalece; la otra recibe un conflicto.
- Intento de editar o eliminar un préstamo ya decidido: se rechaza.
- Un administrador intenta solicitar un préstamo: se le deniega el acceso.
- Se modifican los límites mientras hay solicitudes Pendientes: las ya registradas conservan su monto; los nuevos valores solo se aplican a solicitudes nuevas.
- Límites inválidos al modificarlos (mínimo menor o igual que 0, o mínimo mayor que el máximo): se rechaza con mensaje claro.
- Un usuario es desactivado mientras tiene una sesión vigente: el acceso se pierde a más tardar al expirar la sesión.
- Se consulta repetidamente el estado de los préstamos sin cambios: el resultado es el mismo y rápido; tras una decisión, la siguiente consulta refleja el cambio.
- Un correo se escribe con distinta capitalización al crear un usuario o iniciar sesión: se trata como el mismo correo.

## Requisitos *(obligatorio)*

### Requisitos funcionales

**Acceso y sesión**

- **FR-001**: El sistema MUST permitir iniciar sesión con correo y contraseña, y MUST exigir sesión válida para cualquier función distinta del inicio de sesión.
- **FR-002**: Una sesión MUST expirar a los 10 minutos, sin renovación automática; el usuario MUST iniciar sesión de nuevo.
- **FR-003**: El sistema MUST bloquear temporalmente nuevos intentos de inicio de sesión tras 5 intentos fallidos durante 15 minutos (valores configurables), con un mensaje claro que no revele si el correo existe.
- **FR-004**: Los usuarios MUST poder cerrar sesión de forma segura.
- **FR-005**: El sistema MUST distinguir exactamente dos roles (Admin y Borrower) y MUST denegar el acceso a quien intente una función fuera de su rol.

**Préstamos**

- **FR-006**: Solo un Borrower MUST poder solicitar préstamos, indicando monto y plazo en meses. Un Admin MUST NOT poder solicitarlos.
- **FR-007**: Todo préstamo nuevo MUST nacer en estado Pendiente. Los estados posibles son Pendiente, Aprobado y Rechazado.
- **FR-008**: El monto MUST ser mayor que 0 y estar dentro de los límites mínimo y máximo vigentes.
- **FR-009**: El plazo MUST ser un entero entre 1 y 60 meses.
- **FR-010**: Una solicitud con monto o plazo inválidos MUST rechazarse con un mensaje claro por campo.
- **FR-011**: Un Borrower MUST poder ver la lista y el estado de sus propios préstamos y MUST NOT poder ver ni modificar préstamos de otros.
- **FR-012**: Un Borrower MUST poder editar o eliminar su propio préstamo únicamente mientras esté Pendiente. Un Admin MUST poder eliminar únicamente préstamos Pendientes.
- **FR-013**: Un Admin MUST poder ver todos los préstamos con prestamista, monto y estado.

**Decisiones**

- **FR-014**: Solo un Admin MUST poder aprobar o rechazar, y solo sobre préstamos Pendientes.
- **FR-015**: Una decisión MUST ser final e irreversible: los préstamos Aprobados o Rechazados son inmutables.
- **FR-016**: Cada decisión MUST registrar quién la tomó y cuándo.
- **FR-017**: Si dos administradores deciden a la vez sobre el mismo préstamo, solo una decisión MUST prevalecer y la otra MUST recibir un conflicto claro.
- **FR-018**: Tras aprobar o rechazar, el prestamista MUST ver el nuevo estado sin información desactualizada.
- **FR-019**: Consultar repetidamente el estado de los préstamos MUST ser rápido, y el resultado MUST reflejar siempre el último cambio.

**Usuarios**

- **FR-020**: Un Admin MUST poder crear, consultar, actualizar y desactivar usuarios. No existe registro público.
- **FR-021**: Los usuarios creados por un Admin MUST tener una contraseña de al menos 8 caracteres; el correo MUST ser único.
- **FR-022**: Un usuario desactivado MUST NOT poder iniciar sesión.

**Límites de monto**

- **FR-023**: Los límites mínimo y máximo MUST almacenarse como un parámetro de configuración persistente, no fijos en el programa.
- **FR-024**: Un Admin MUST poder consultar y modificar esos límites, y el cambio MUST aplicarse a las solicitudes nuevas sin redesplegar.

**Datos iniciales**

- **FR-025**: El sistema MUST incluir como datos iniciales un prestamista (`usuario@test.com` / `123`, contraseña solo de ejemplo), un administrador (`admin@test.com` / `123`) y los límites por defecto (mínimo 1.000, máximo 100.000).

**Interfaz (referencia: 3 pantallas principales, más gestión de usuarios)**

- **FR-026**: La pantalla de Login MUST pedir correo y contraseña, con validaciones.
- **FR-027**: La vista de prestamista MUST ofrecer un formulario para solicitar (monto y plazo) y una lista "Mis préstamos" con estado.
- **FR-028**: La vista de administrador MUST mostrar la lista de solicitudes con las acciones Aprobar y Rechazar en las pendientes, y permitir gestionar los límites de monto.
- **FR-029**: La pantalla de usuarios del administrador MUST permitir crear, consultar, actualizar y desactivar usuarios.
- **FR-030**: Las rutas de administrador MUST estar protegidas para usuarios sin ese rol.

### Entidades clave

- **Usuario**: persona con acceso al sistema. Atributos: correo (único), nombre, rol (Admin o Borrower), estado activo/desactivado y credencial protegida.
- **Préstamo**: solicitud de un prestamista. Atributos: prestamista dueño, monto, plazo en meses, estado (Pendiente, Aprobado, Rechazado) y fechas de creación y de decisión.
- **Decisión (registro de auditoría)**: constancia de que un préstamo fue aprobado o rechazado. Relaciona al préstamo con el administrador que decidió, el resultado y el momento.
- **Parámetro de límites**: configuración con el monto mínimo y máximo vigentes para solicitar préstamos.

## Criterios de éxito *(obligatorio)*

### Resultados medibles

- **SC-001**: Un prestamista puede iniciar sesión y registrar una solicitud válida en menos de 1 minuto.
- **SC-002**: El 100 % de los intentos de un prestamista por ver o modificar préstamos de otro prestamista son denegados.
- **SC-003**: El 100 % de las consultas posteriores a una decisión muestran el estado nuevo del préstamo.
- **SC-004**: Ante decisiones simultáneas sobre un mismo préstamo, en el 100 % de los casos prevalece exactamente una y el estado final es único y consistente.
- **SC-005**: El 100 % de las decisiones queda registrado con el administrador que la tomó y la fecha.
- **SC-006**: El 95 % de las consultas de estado de préstamos devuelve resultado en menos de 1 segundo.
- **SC-007**: El 100 % de las solicitudes con datos inválidos recibe un mensaje que identifica el campo con el error.
- **SC-008**: Tras 5 intentos de inicio de sesión fallidos, el 100 % de los intentos siguientes durante 15 minutos son bloqueados con un mensaje que no revela si el correo existe.
- **SC-009**: Una sesión deja de ser válida a los 10 minutos en el 100 % de los casos.
- **SC-010**: Un cambio de límites se aplica a la siguiente solicitud nueva, sin interrupción del servicio ni redespliegue.

## Supuestos

- Los usuarios acceden a través de la interfaz web (3 pantallas principales más la gestión de usuarios) o directamente por la API REST; el contrato entre ambos es esa API.
- La contraseña `123` de los datos iniciales es solo de ejemplo para la demostración.
- Los montos se expresan en una única moneda, sin conversión.
- Un préstamo no se "desembolsa": Aprobado es solo un estado administrativo.
- Los límites cambiados solo aplican a solicitudes nuevas; las ya registradas conservan su monto aunque queden fuera de los nuevos límites.
- Desactivar un usuario no borra su historial: sus préstamos y decisiones se conservan.
- El correo se compara sin distinguir mayúsculas y minúsculas.
- Se asume que un administrador no puede desactivar su propia cuenta, para evitar dejar el sistema sin administradores.
- Un usuario cuenta con un único rol.
- Dependencia: existe una base de datos persistente donde se almacenan usuarios, préstamos, decisiones y límites.

## Fuera de alcance

- Cálculo de intereses, cuotas, desembolsos y pagos.
- Registro público de usuarios, recuperación de contraseña y notificaciones por correo.
