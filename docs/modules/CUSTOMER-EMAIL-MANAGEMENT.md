# Administración de correos electrónicos de Customer

## Estado y alcance

La administración independiente de correos está implementada. El correo no
forma parte del readiness para capturar contratos: `CustomerProfileReadiness`
continúa evaluando únicamente datos generales, domicilio, teléfono y cuenta.

La administración es independiente de domicilios, teléfonos y cuentas. El
alta inicial ya no recibe contacto, correo ni usos, no reserva
`CPERSONA_EMAIL`, no escribe `CPERSONA_EMAIL` ni `KEMAIL_USO` y no consulta los
catálogos 244 o 248. Las propiedades antiguas se rechazan por el contrato
JSON estricto. Después de obtener `PNA_FL_PERSONA`, el usuario agrega uno o
varios correos desde la sección Correos.

## Implementación actual revisada

El flujo independiente vigente es:

- `CustomerEmailRequest` recibe contacto, correo opcional en edición y usos.
- `CustomerEmailManagement` valida formato, usos y reglas de estado.
- `LegacyCustomerEmailWriteRepository` reserva `CCATCONSEC` para
  `CPERSONA_EMAIL`, inserta una fila activa y una fila por uso dentro de la
  transacción de la operación independiente.
- `LegacyCustomerReadRepository` consulta sólo correos activos
  (`MAI_FG_STATUS = 1`) en una consulta separada, sin multiplicar personas.
- `CustomerEmail` y `CustomerEmailResponse` exponen actualmente `emailId`,
  contacto y correo completo en el detalle autorizado. No se incluyen
  correos en el resultado de búsqueda general.

La exposición actual del correo completo deberá conservarse sólo para el
detalle autorizado cuando se implemente el módulo; no debe copiarse a listas,
logs, errores, almacenamiento del navegador ni respuestas fuera de alcance.

## Evidencia confirmada disponible

La documentación de Customer Create y el código actual confirman el uso de
las siguientes columnas, pero no sustituyen una extracción completa de
`sys.columns`, índices, defaults y triggers:

| Tabla | Columna | Uso observado | Estado de evidencia |
|---|---|---|---|
| `CPERSONA_EMAIL` | `MAI_FL_CVE` | Identificador generado por `CCATCONSEC` | Confirmado por INSERT y lectura |
| `CPERSONA_EMAIL` | `PNA_FL_PERSONA` | Relación lógica con Customer | Confirmado por INSERT y lectura |
| `CPERSONA_EMAIL` | `MAI_DS_CONTACTO` | Contacto asociado al correo | Confirmado por INSERT y lectura |
| `CPERSONA_EMAIL` | `MAI_DS_EMAIL` | Dirección de correo | Confirmado por INSERT y lectura |
| `CPERSONA_EMAIL` | `MAI_FG_STATUS` | La lectura considera activo el valor `1` | Confirmado por repositorio actual; catálogo de otros estados pendiente |
| `CPERSONA_EMAIL` | `USR_CL_CVE` | Usuario Legacy de modificación/alta | Confirmado por INSERT actual |
| `CPERSONA_EMAIL` | `MAI_FE_ULTMOD` | Fecha técnica de modificación | Confirmado por INSERT actual; uso como token pendiente |
| `CPERSONA_EMAIL` | `MAI_FG_OMITIR_ENVIO` | El alta actual escribe `0` | Confirmado como comportamiento actual, significado funcional pendiente |
| `KEMAIL_USO` | `PAR_CL_VALOR` | Código de uso | Confirmado por INSERT actual |
| `KEMAIL_USO` | `MAI_FL_CVE` | Relación con `CPERSONA_EMAIL` | FK documentada hacia `MAI_FL_CVE` |
| `KEMAIL_USO` | `USR_CL_CVE` | Usuario Legacy | Confirmado por esquema documentado |
| `KEMAIL_USO` | `USO_FE_MODIFICACION` | Fecha del uso | Confirmado por esquema documentado |

No se afirma aquí la nulabilidad, longitud, default, PK, índice o trigger de
ninguna columna que no esté respaldada por una extracción autoritativa. La
matriz completa queda pendiente de las consultas de sólo lectura indicadas
más abajo.

## Matriz de campos para el contrato futuro

Los nombres de aplicación son deliberadamente neutrales a Legacy. Las
longitudes definitivas se deben tomar de `sys.columns` antes de implementar.

| Propiedad | Dirección | Tipo propuesto | Regla de exposición | Persistencia Legacy | Pendiente |
|---|---|---|---|---|---|
| `emailId` | respuesta | `int` | Detalle autorizado; nunca URL ni log | `MAI_FL_CVE` | Confirmar PK |
| `contact` | request/respuesta | `string` nullable | Detalle autorizado; no listado general | `MAI_DS_CONTACTO` | Confirmar longitud y nulabilidad |
| `email` | request/respuesta | `string` | Detalle autorizado; nunca log ni error | `MAI_DS_EMAIL` | Confirmar longitud, nulabilidad y normalización |
| `status` | request/respuesta | estado controlado | No aceptar códigos arbitrarios | `MAI_FG_STATUS` | Confirmar catálogo y valores administrables |
| `usageCodes` | request/respuesta | lista de usos controlados | Sólo catálogo 244 vigente | `KEMAIL_USO.PAR_CL_VALOR` | Confirmar duplicidad y vigencia |
| `modifiedAt` | respuesta/request de mutación | fecha técnica | Sólo token de concurrencia | `MAI_FE_ULTMOD` | Confirmar precisión y uso Legacy |
| `omitDelivery` | interno | booleano | No exponer si no es necesario | `MAI_FG_OMITIR_ENVIO` | Confirmar significado y reglas |

No se propone un correo principal: el modelo actual no tiene un indicador
confirmado para ello. Tampoco se hará que un correo sea requisito de
readiness.

## `KEMAIL_USO` y catálogo 244

La evidencia ya documentada confirma las columnas `PAR_CL_VALOR` (int),
`MAI_FL_CVE` (int), `USR_CL_CVE` (varchar(25)) y
`USO_FE_MODIFICACION` (datetime), todas nullable según el esquema recibido.
También está documentada la FK de `MAI_FL_CVE` hacia
`CPERSONA_EMAIL.MAI_FL_CVE`; no se confirmó una PK física de `KEMAIL_USO`.

El catálogo funcional aprobado previamente es:

| Código | Descripción | Situación |
|---:|---|---|
| 1 | Envío de facturas | Verificar vigencia actual |
| 2 | Envío de estado de cuenta | Verificar vigencia actual |
| 3 | Salesforce | Verificar vigencia actual |

Estos valores no deben hardcodearse en el adaptador hasta verificar que
`CPARAMETRO` catálogo `244` los mantiene activos. La API futura debe aceptar
únicamente usos vigentes devueltos por el catálogo y rechazar duplicados o
valores desconocidos.

## Reglas funcionales confirmadas y pendientes

Confirmado por el alta actual:

- puede existir más de un uso para un correo;
- el alta exige al menos un uso y crea una fila por uso;
- `MAI_FG_STATUS = 1` es el criterio actual de correo activo;
- `MAI_FG_OMITIR_ENVIO` se inicializa en `0` en el alta moderna;
- el uso recibe `LegacyUserCode` y una fecha de operación;
- `MAI_FL_CVE` se obtiene mediante `CCATCONSEC` en altas.

Pendiente de confirmación antes de escribir:

- todos los campos NOT NULL de ambas tablas y sus defaults;
- estados administrables distintos de `1` y si el estado `0` es histórico;
- si existe correo predeterminado o una prioridad funcional;
- si una persona puede permanecer sin correo activo;
- si se permite correo duplicado dentro de una persona;
- si la edición reemplaza, conserva o elimina usos;
- si la eliminación es lógica mediante estado o física, que no se asumirá;
- significado exacto de `MAI_FG_OMITIR_ENVIO` y relación con la lista negra
  del catálogo 248;
- actividad de bitácora para alta, edición, activación y desactivación;
- si `MAI_FE_ULTMOD` permite concurrencia optimista y cómo compara Legacy la
  precisión de la fecha;
- si `KEMAIL_USO` requiere una operación de limpieza o conserva historial.

No se deducirán estas reglas por frecuencia de valores ni por la existencia
de una columna con nombre parecido.

## Consultas de sólo lectura necesarias

Estas consultas son plantillas para ejecutar únicamente con autorización,
sin valores de correo ni datos personales. En esta etapa no se ejecutaron.

### Columnas, tipos, nulabilidad y defaults

```sql
SELECT
    s.name AS SchemaName, t.name AS TableName, c.column_id,
    c.name AS ColumnName, ty.name AS SqlType, c.max_length,
    c.precision, c.scale, c.is_nullable, c.is_identity,
    dc.definition AS DefaultDefinition
FROM sys.tables AS t
JOIN sys.schemas AS s ON s.schema_id = t.schema_id
JOIN sys.columns AS c ON c.object_id = t.object_id
JOIN sys.types AS ty ON ty.user_type_id = c.user_type_id
LEFT JOIN sys.default_constraints AS dc
  ON dc.parent_object_id = c.object_id
 AND dc.parent_column_id = c.column_id
WHERE s.name = 'dbo'
  AND t.name IN ('CPERSONA_EMAIL', 'KEMAIL_USO')
ORDER BY t.name, c.column_id;
```

### PK, índices, FK y triggers

```sql
SELECT i.name AS IndexName, i.type_desc, i.is_unique,
       i.is_primary_key, i.is_unique_constraint, ic.key_ordinal,
       c.name AS ColumnName, ic.is_included_column
FROM sys.tables AS t
JOIN sys.schemas AS s ON s.schema_id = t.schema_id
JOIN sys.indexes AS i ON i.object_id = t.object_id
JOIN sys.index_columns AS ic ON ic.object_id = i.object_id
 AND ic.index_id = i.index_id
JOIN sys.columns AS c ON c.object_id = ic.object_id
 AND c.column_id = ic.column_id
WHERE s.name = 'dbo' AND t.name IN ('CPERSONA_EMAIL', 'KEMAIL_USO')
ORDER BY t.name, i.name, ic.key_ordinal, ic.index_column_id;

SELECT fk.name, OBJECT_NAME(fkc.parent_object_id) AS ChildTable,
       COL_NAME(fkc.parent_object_id, fkc.parent_column_id) AS ChildColumn,
       OBJECT_NAME(fkc.referenced_object_id) AS ParentTable,
       COL_NAME(fkc.referenced_object_id, fkc.referenced_column_id) AS ParentColumn,
       fk.is_disabled
FROM sys.foreign_keys AS fk
JOIN sys.foreign_key_columns AS fkc
  ON fkc.constraint_object_id = fk.object_id
WHERE OBJECT_SCHEMA_NAME(fkc.parent_object_id) = 'dbo'
  AND OBJECT_NAME(fkc.parent_object_id) IN ('CPERSONA_EMAIL', 'KEMAIL_USO');

SELECT s.name AS SchemaName, t.name AS TableName, tr.name AS TriggerName,
       tr.is_disabled, tr.is_instead_of_trigger
FROM sys.triggers AS tr
JOIN sys.tables AS t ON t.object_id = tr.parent_id
JOIN sys.schemas AS s ON s.schema_id = t.schema_id
WHERE s.name = 'dbo' AND t.name IN ('CPERSONA_EMAIL', 'KEMAIL_USO');
```

### Catálogos y listas negras, sólo códigos y descripciones

```sql
SELECT PAR_FL_CVE, PAR_CL_VALOR, PAR_DS_DESCRIPCION,
       PAR_FG_STATUS, PAR_FG_REGDEFAULT
FROM dbo.CPARAMETRO
WHERE PAR_FL_CVE IN (244, 248)
ORDER BY PAR_FL_CVE, PAR_CL_VALOR;
```

### Distribuciones sin PII

```sql
SELECT MAI_FG_STATUS, MAI_FG_OMITIR_ENVIO, COUNT_BIG(*) AS Total
FROM dbo.CPERSONA_EMAIL
GROUP BY MAI_FG_STATUS, MAI_FG_OMITIR_ENVIO;

SELECT PAR_CL_VALOR, COUNT_BIG(*) AS Total
FROM dbo.KEMAIL_USO
GROUP BY PAR_CL_VALOR;

WITH EmailCounts AS
(
    SELECT PNA_FL_PERSONA, COUNT_BIG(*) AS EmailCount
    FROM dbo.CPERSONA_EMAIL
    GROUP BY PNA_FL_PERSONA
)
SELECT EmailCount, COUNT_BIG(*) AS PeopleCount
FROM EmailCounts
GROUP BY EmailCount;
```

La consulta de cardinalidad devuelve sólo cantidades agrupadas y no expone
identificadores de persona. Nunca se deben consultar direcciones de correo
completas para distribución.

## Diseño propuesto de API

```text
GET  /api/v1/customers/{personId}/emails
POST /api/v1/customers/{personId}/emails
PUT  /api/v1/customers/{personId}/emails/{emailId}
POST /api/v1/customers/{personId}/emails/{emailId}/activate
POST /api/v1/customers/{personId}/emails/{emailId}/deactivate
```

- GET requiere autenticación, tenant seleccionado y `customers.read`.
- Las mutaciones requieren `customers.write`, antiforgery, tenant coincidente
  con `Deployment__TenantCode`, membresía activa y `LegacyUserCode` de esa
  membresía.
- Las escrituras se limitarán a Development, conexión Legacy de escritura y
  verificación efectiva de `DB_NAME() = pr_t`, con una transacción Dapper.
- No se usará `AllowedCompanyIds` para Customers: el alcance es el tenant y
  despliegue configurados.
- La persona y el correo deben validarse por pertenencia a la instalación;
  persona inexistente o correo fuera de esa persona devuelve `404`.

### Respuesta y privacidad

El detalle autorizado puede devolver el correo completo únicamente si la
política de producto lo aprueba. La lista general de clientes no incluirá
correos. Las respuestas de error no incluirán correo, contacto ni payload.
No se guardarán correos en `localStorage` o `sessionStorage`, ni se usarán
en URLs. Los logs sólo incluirán etapa, tipo de excepción, número SQL cuando
corresponda y correlation ID.

## Mutaciones, concurrencia y respuestas

Cada alta, edición o cambio de estado debe usar una sola transacción. El alta
reservará `MAI_FL_CVE` mediante `CCATCONSEC`; la edición y los cambios de
estado no reservarán consecutivos. La bitácora se escribirá dentro de la
misma transacción, con referencia técnica que no contenga correo ni contacto.

No se implementará eliminación física. La estrategia de desactivación queda
pendiente de confirmar en Legacy. `PUT`, activar y desactivar recibirán
`expectedModifiedAt` sólo si `MAI_FE_ULTMOD` se confirma como token viable;
una actualización de cero filas produciría `409 email_modified`.

Respuestas propuestas:

| Código | Uso |
|---:|---|
| 200 | GET, PUT y cambios de estado exitosos |
| 201 | POST exitoso |
| 400 | correo/formato/uso/catálogo inválido |
| 401 | usuario anónimo |
| 403 | permiso, tenant o membresía no autorizada |
| 404 | persona o correo inexistente/fuera de alcance |
| 409 | `email_duplicate` o `email_modified`, después de confirmar reglas |
| 503 | escritura no configurada o base efectiva distinta de `pr_t` |

Errores inesperados deben llegar al manejador global como 500; no se deben
convertir indiscriminadamente en 503.

## UX y compatibilidad con el alta inicial

El expediente mostrará una tarjeta responsive de correos con estados activo e
inactivo y usos funcionales descritos en lenguaje amigable. El panel de alta
y edición será accesible, evitará doble envío, mostrará carga/éxito/error y
refrescará la colección después de mutar. El correo completo no se
precargará en ningún formulario de edición si la política decide ocultarlo;
la estrategia de reemplazo debe aprobarse con Legacy.

El detalle de un cliente recién creado muestra la sección Correos vacía y la
acción “Agregar correo”. Los clientes existentes conservan sus correos y se
siguen mostrando en el detalle autorizado; no se realiza migración ni
actualización masiva.

## Pruebas previstas

Backend y adaptador:

- esquema, PK/FK/índices/defaults/triggers confirmados sin SQL de escritura;
- GET vacío, múltiple y sólo en detalle autorizado;
- 401, 403, 404 y tenant inválido;
- POST 201 y consecutivo sólo en alta;
- PUT conserva o reemplaza según contrato aprobado;
- activación/desactivación y estados Legacy;
- `email_duplicate`, `email_modified` y 503;
- antiforgery, tenant, membresía y `LegacyUserCode`;
- transacción y rollback;
- catálogo 244 vigente, usos desconocidos y duplicados;
- catálogo 248 y `MAI_FG_OMITIR_ENVIO` cuando se confirme su significado;
- SQL parametrizado, mapeo mediante fila de infraestructura y logs sin PII;
- JSON sin correo completo en listados generales ni campos no aprobados.

Frontend:

- tarjeta y lista vacía/múltiple;
- panel accesible, validación, carga, errores y doble envío;
- alta compatible con el correo inicial;
- edición sin almacenar correo en navegador;
- activación/desactivación, refresco y estados;
- ausencia de correo en búsqueda general y presencia sólo en detalle autorizado.

## Bloqueos y decisiones pendientes

1. Obtener y revisar el esquema completo de `CPERSONA_EMAIL` y `KEMAIL_USO`,
   incluyendo PK, índices, defaults, FK y triggers.
2. Confirmar vigencia actual de los códigos 1, 2 y 3 del catálogo 244 y
   obtener el catálogo 248 sin consultar direcciones de correo.
3. Confirmar por código Legacy el significado de `MAI_FG_OMITIR_ENVIO`, la
   lista negra, las actividades de bitácora y si la auditoría es transaccional.
4. Confirmar duplicidad, eliminación lógica, estados administrables y
   concurrencia con `MAI_FE_ULTMOD`.
5. Decidir si un correo completo puede devolverse en detalle autorizado y
   cómo se reemplaza sin precargarlo en edición.
6. Definir la relación entre la fila creada en el alta inicial y el futuro
   panel, evitando duplicados.

Los bloqueos anteriores son de confirmación operativa del esquema y del
comportamiento Legacy; la implementación local queda sujeta a verificarlos
antes de una prueba manual contra una base autorizada.

## Reglas aprobadas para la implementación

La evidencia funcional queda aprobada con estas reglas:

- una persona puede tener uno o varios correos y no existe correo
  predeterminado;
- una persona puede permanecer sin correos activos; correo no participa en
  readiness;
- `MAI_FG_STATUS = 1` es activo, `2` es inactivo y no se eliminan físicamente
  filas de `CPERSONA_EMAIL`;
- el correo es obligatorio, máximo 250 caracteres y con formato válido;
  contacto es opcional, máximo 250 caracteres;
- `MAI_FG_OMITIR_ENVIO` siempre se escribe como `0` y no se expone en API/UI;
- `USR_CL_CVE` proviene exclusivamente de `LegacyUserCode` de la membresía
  activa; `MAI_FE_ULTMOD` se usa para concurrencia optimista;
- los usos se consultan dinámicamente desde catálogo `244`, aceptando sólo
  valores activos mayores que cero. Alta y edición requieren al menos uno,
  rechazan desconocidos, inactivos y duplicados, y reemplazan sólo los usos
  del correo objetivo dentro de la misma transacción;
- al desactivar se conservan los usos; no se corrigen históricos de otras
  personas;
- el correo activo duplicado se rechaza únicamente dentro de la misma
  persona, comparando `Trim` sin distinguir mayúsculas/minúsculas. En edición
  se excluye el propio `MAI_FL_CVE`; el código es `email_duplicate`;
- el catálogo 248 se valida en backend para correos nuevos o reemplazados,
  sin devolver la lista al frontend y con error genérico 400. No se aplica
  retroactivamente a correos históricos no modificados;
- se conserva el trigger `TFSM_ACTUALIZAR_CORREO_HIST`. Sólo se incluye
  `MAI_DS_EMAIL` en el `UPDATE` cuando el correo cambia realmente; contacto,
  usos o estado no generan un cambio de correo falso;
- PUT, activar y desactivar reciben `expectedModifiedAt`. Una actualización
  condicionada por `MAI_FE_ULTMOD` de cero filas devuelve `409
  email_modified`; la nueva fecha se genera dentro de la operación;
- la bitácora usa actividad 4 en alta y 5 en edición, activación y
  desactivación, dentro de la misma transacción y sin correo ni contacto en
  la referencia;
- el correo inicial de Crear cliente se reutiliza en la tarjeta sin crear
  otro registro y conserva el uso inicial de Envío de facturas.

## Contrato de implementación aprobado

```text
GET  /api/v1/customers/{personId}/emails
POST /api/v1/customers/{personId}/emails
PUT  /api/v1/customers/{personId}/emails/{emailId}
POST /api/v1/customers/{personId}/emails/{emailId}/activate
POST /api/v1/customers/{personId}/emails/{emailId}/deactivate
GET  /api/v1/catalogs/email-uses
```

GET y el catálogo requieren `customers.read`, autenticación y tenant
seleccionado válido. Las mutaciones requieren `customers.write`, antiforgery,
tenant coincidente con el despliegue, `LegacyUserCode`, Development, conexión
de escritura y `DB_NAME() = pr_t`. Las respuestas y errores no contienen
correos completos fuera del GET autorizado del expediente; no se usa
`localStorage` ni `sessionStorage`.

## Primera implementación

La primera implementación queda distribuida así:

- `CustomerEmailManagement.cs` contiene los comandos, contratos de repositorio,
  reglas de normalización y estados controlados.
- `LegacyCustomerEmailReadRepository` materializa filas privadas de
  infraestructura y después las mapea al modelo del módulo; las consultas son
  exclusivamente `SELECT` y no exponen datos en logs.
- `LegacyCustomerEmailWriteRepository` mantiene la transacción, los
  consecutivos, la auditoría 4/5, la concurrencia y la protección de
  `MAI_FG_OMITIR_ENVIO = 0`. El `UPDATE` de metadatos no incluye la columna de
  correo, para preservar el comportamiento del trigger histórico cuando el
  correo no cambia.
- `CustomerEmailEndpoints` publica los cinco recursos de administración y el
  catálogo dinámico de usos. Los handlers declaran servicios explícitamente y
  conservan las respuestas controladas sin convertir excepciones inesperadas
  en 503.
- `CustomersView` muestra la tarjeta de correos y obtiene el catálogo 244 en
  tiempo de ejecución. La edición deja vacío el correo para conservarlo y no
  lo precarga en el formulario; la creación selecciona inicialmente el uso
  cuya descripción corresponde a facturación, sin fijar su código en el
  frontend.

La lista negra 248 se consulta únicamente para validar altas o reemplazos y la
columna concreta usada para la descripción debe verificarse contra el esquema
del despliegue antes de una prueba manual. No se ejecutaron SQL, POST,
migraciones ni IdentityAdmin durante esta etapa.
