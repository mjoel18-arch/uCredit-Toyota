# Administración múltiple de teléfonos

## Estado de esta etapa

Esta entrega es únicamente de análisis. No agrega endpoints, escrituras
Legacy, migraciones ni cambios de esquema. La implementación queda
condicionada a confirmar los metadatos y catálogos indicados como pendientes.

## Evidencia disponible

La consulta actual de Customers carga `dbo.CTELEFONO` por separado y sólo
lee teléfonos activos (`TFN_FG_STATUS = 1`). El modelo conserva la colección
activa y calcula el principal con:

```text
TFN_FG_REGDEFAULT = 1
ORDER BY TFN_FL_CVE ASC
```

Por ello, si existen varios teléfonos activos predeterminados, la API actual
elige defensivamente el menor `TFN_FL_CVE`. La existencia de un teléfono
predeterminado no forma parte del readiness; sólo se requiere un teléfono
activo.

El código Legacy revisado es `sd_clsPersona.vb`, método
`ActualizaTelefono`, y la pantalla `su_MtoTelefono.aspx.vb`:

- el alta solicita persona, tipo de teléfono, lada,
  número, extensión, razón de inactividad, estatus, predeterminado y nombre
  de contacto;
- antes de crear, si la persona ya tiene cualquier fila en `CTELEFONO`, el
  valor de predeterminado respeta la selección recibida; si no tiene filas,
  el primer teléfono se fuerza a predeterminado;
- cuando se solicita predeterminado, Legacy desmarca los predeterminados de
  la persona dentro de la transacción;
- el alta reserva `TFN_FL_CVE` mediante `ObtenConsecutivo("CTELEFONO")`;
- la modificación actualiza por `TFN_FL_CVE`, cambia
  `TFN_FG_REGDEFAULT` directamente y no muestra una condición de fecha
  optimista en el SQL Legacy revisado;
- la pantalla permite desactivar un teléfono y también conserva registros
  históricos mediante `TFN_FG_STATUS`; el método Legacy antiguo incluye una
  eliminación física, pero esa operación no será expuesta por uCredit;
- la pantalla usa `CommandArgumentControl = 14` para autorización de
  Guardar. Ese número no se interpreta automáticamente como
  `ATV_FL_CVE`; la actividad efectiva de bitácora aún debe confirmarse en
  `KACCION`/`Seguridad.Bitacora`.

La lectura Legacy relaciona los teléfonos con la persona mediante
`CTELEFONO.PNA_FL_PERSONA = CPERSONA.PNA_FL_PERSONA`. La evidencia revisada
confirma esta relación lógica de consulta; todavía no confirma una FK física.

## Matriz de columnas de `CTELEFONO`

Los nombres siguientes aparecen en el método Legacy de alta y en la consulta
de detalle. Los tipos, longitudes, nulabilidad y defaults no deben inferirse
del código VB; requieren la consulta de metadatos incluida más adelante.

| Columna | Uso observado | Tipo/longitud | Nullable/default | Estado |
|---|---|---|---|---|
| `TFN_FL_CVE` | Identificador y consecutivo | Pendiente | Pendiente | Debe confirmarse PK y columna de `CCATCONSEC` |
| `PNA_FL_PERSONA` | Relación con Customer | Pendiente | Pendiente | Relación lógica confirmada por código |
| `TTL_FL_CVE` | Tipo de teléfono | Pendiente | Pendiente | Catálogo `CTTELEFONO` pendiente |
| `DMO_FL_CVE` | Asociación física histórica | `int` | No (`pr_t` efectiva) | No pertenece al contrato HTTP; la aplicación no lo lee ni lo expone |
| `TFN_CL_LARGA_DISTANCIA` | Prefijo/larga distancia | Pendiente | Pendiente | El Legacy puede enviar cadena vacía |
| `TFN_CL_TELEFONO` | Número telefónico | Pendiente | Pendiente | PII; no se incluirá en logs ni fixtures reales |
| `TFN_CL_EXTENSION` | Extensión | Pendiente | Pendiente | Nulabilidad y longitud pendientes |
| `TFN_DS_RAZON_INACTIVO` | Motivo de inactividad | Pendiente | Pendiente | Regla requerida al desactivar pendiente |
| `TFN_FG_STATUS` | Estado | Pendiente | `1` es activo por consulta/readiness; catálogo completo pendiente | Confirmar valores inactivos |
| `TFN_FG_REGDEFAULT` | Predeterminado | Pendiente | `1` representa predeterminado por lectura/código | No hay unicidad física confirmada |
| `TFN_FE_ULTMOD` | Fecha de modificación | Pendiente | Pendiente | Candidato para concurrencia; debe confirmarse precisión y nullability |
| `USR_CL_CVE` | Usuario Legacy | Pendiente | Pendiente | Se obtiene de la membresía; nunca se expone |
| `TFN_CL_LADA` | Lada | Pendiente | Pendiente | PII; no se registra |
| `TFN_DS_CONTACTO` | Nombre de contacto | Pendiente | Pendiente | PII; no se registra |

No se agregará una columna, default o transformación hasta confirmar
`sys.columns` y las restricciones reales.

## Catálogo `CTTELEFONO`

El código Legacy usa `TTL_FL_CVE` como clave y une `CTELEFONO` con
`CTTELEFONO` por esa columna. La pantalla carga los tipos mediante
`ObtenTTelefonoDS(-1, 1, 2, ...)`; al editar, obtiene además la descripción
del tipo y una regla de celular usada para decidir si la lada se captura.

Todavía no está confirmado:

- esquema completo de `CTTELEFONO`;
- PK, FK, índices y columnas de vigencia;
- valores autorizados y descripciones para casa, oficina, celular, fax,
  fiscal u otros;
- si `CELULAR` es una columna física o un valor derivado por el servicio;
- si el catálogo filtra por estatus `1` y qué significa el segundo argumento
  `2` de `ObtenTTelefonoDS`.

La UI moderna no mostrará nombres técnicos ni permitirá enviar una clave que
no haya sido validada contra el catálogo activo. Las categorías sólo podrán
publicarse después de confirmar los valores reales.

## Predeterminado y datos históricos

La evidencia Legacy confirma estas reglas operativas:

1. el primer teléfono se crea predeterminado;
2. al elegir un nuevo predeterminado se desmarcan los anteriores de la misma
   persona dentro de la transacción;
3. el estado activo y el predeterminado son conceptos separados;
4. no se confirmó una restricción única física para
   `PNA_FL_PERSONA + TFN_FG_REGDEFAULT`;
5. la lectura actual conserva la selección defensiva por menor
   `TFN_FL_CVE` cuando hay duplicados históricos.

No se corregirán masivamente duplicados. La UI los presentará con el
distintivo `Predeterminado` en cada fila y el detalle mantendrá la selección
defensiva existente. Cuando el usuario elija un nuevo predeterminado, la
transacción moderna desmarcará los demás y dejará uno activo predeterminado.

Queda pendiente confirmar si Legacy permite desactivar el único teléfono
predeterminado y si la regla aprobada para domicilios de “reemplazo
obligatorio” también aplica a teléfonos. Hasta contar con esa decisión, no
se debe inventar un código `409` ni permitir que la UI quite el último
predeterminado sin una regla backend explícita.

## Concurrencia

`TFN_FE_ULTMOD` es el candidato natural porque el método de lectura y el
Legacy de modificación lo actualizan. Sin embargo, faltan sus metadatos
exactos y evidencia de que tenga precisión suficiente para un token de
concurrencia.

Diseño propuesto, sujeto a confirmación:

- PUT, activar y desactivar reciben `expectedModifiedAt`;
- la condición de actualización incluye `PNA_FL_PERSONA`, `TFN_FL_CVE` y
  `TFN_FE_ULTMOD`;
- cero filas actualizadas devuelve `409 phone_modified`;
- el cambio de predeterminado y la actualización del teléfono ocurren en la
  misma transacción;
- el alta no usa `expectedModifiedAt` y sí reserva un consecutivo.

## Diseño preliminar de endpoints

```text
GET  /api/v1/customers/{personId}/phones
POST /api/v1/customers/{personId}/phones
PUT  /api/v1/customers/{personId}/phones/{phoneId}
POST /api/v1/customers/{personId}/phones/{phoneId}/activate
POST /api/v1/customers/{personId}/phones/{phoneId}/deactivate
```

`GET` requiere `customers.read`. Las mutaciones requieren
`customers.write`, antiforgery, tenant seleccionado coincidente con
`Deployment__TenantCode`, `LegacyUserCode` de la membresía activa, entorno
Development, conexión Legacy de escritura y las guardas explícitas de
`pr_t`.

El repositorio de teléfonos pertenecerá al módulo Customers y su adaptador
será `LegacySql`. No se usará `AllowedCompanyIds`: Customers está delimitado
por el tenant/despliegue y no existe una relación empresa-persona confirmada.

Respuestas preliminares:

- `401` anónimo;
- `403` permiso ausente o tenant inválido;
- `404` persona o teléfono inexistente;
- `400` payload o tipo de teléfono inválido;
- `409` concurrencia o conflicto de predeterminado, sólo con códigos aún por
  aprobar;
- `503` escritura no configurada o base distinta de `pr_t`.

Los DTOs no expondrán campos de auditoría, cadenas de conexión ni valores
internos. El número, lada y extensión sólo se devolverán a usuarios
autorizados por la consulta y nunca aparecerán en logs, excepciones,
fixtures reales, URLs, `localStorage` o `sessionStorage`.

## Transacciones y auditoría propuestas

Cada mutación usará una sola transacción Dapper:

1. validar persona, tenant, actor y configuración de escritura;
2. validar tipo contra `CTTELEFONO` activo;
3. reservar `TFN_FL_CVE` sólo en alta;
4. bloquear y desmarcar predeterminados anteriores cuando corresponda;
5. insertar o actualizar el teléfono con parámetros tipados;
6. escribir `KBITACORA` dentro de la misma transacción;
7. commit; ante cualquier error, rollback.

La actividad y el formato de referencia no se fijarán hasta confirmar el
valor efectivo de `ATV_FL_CVE`, la configuración de `KACCION` y el flujo de
`Seguridad.Bitacora`. La referencia moderna no contendrá números
telefónicos, lada, extensión, contacto ni payload.

## Consultas de sólo lectura pendientes

Estas consultas deben ejecutarse únicamente con autorización explícita y
contra una base de pruebas segura. En esta etapa sólo quedan documentadas.

### Esquema, defaults y PK de `CTELEFONO`

```sql
SELECT c.column_id, c.name AS ColumnName, ty.name AS SqlType,
       c.max_length, c.precision, c.scale, c.is_nullable,
       c.is_identity, dc.definition AS DefaultDefinition
FROM sys.tables AS t
JOIN sys.schemas AS s ON s.schema_id = t.schema_id
JOIN sys.columns AS c ON c.object_id = t.object_id
JOIN sys.types AS ty ON ty.user_type_id = c.user_type_id
LEFT JOIN sys.default_constraints AS dc
  ON dc.parent_object_id = c.object_id
 AND dc.parent_column_id = c.column_id
WHERE s.name = 'dbo' AND t.name = 'CTELEFONO'
ORDER BY c.column_id;
```

### Índices, restricciones únicas y FK

```sql
SELECT i.name AS IndexName, i.type_desc, i.is_unique,
       i.is_primary_key, i.is_unique_constraint, ic.key_ordinal,
       c.name AS ColumnName, ic.is_included_column
FROM sys.tables AS t
JOIN sys.schemas AS s ON s.schema_id = t.schema_id
JOIN sys.indexes AS i ON i.object_id = t.object_id
JOIN sys.index_columns AS ic
  ON ic.object_id = i.object_id AND ic.index_id = i.index_id
JOIN sys.columns AS c
  ON c.object_id = ic.object_id AND c.column_id = ic.column_id
WHERE s.name = 'dbo' AND t.name = 'CTELEFONO'
ORDER BY i.name, ic.key_ordinal, ic.index_column_id;

SELECT fk.name AS ForeignKeyName,
       OBJECT_SCHEMA_NAME(fkc.parent_object_id) AS ChildSchema,
       OBJECT_NAME(fkc.parent_object_id) AS ChildTable,
       COL_NAME(fkc.parent_object_id, fkc.parent_column_id) AS ChildColumn,
       OBJECT_SCHEMA_NAME(fkc.referenced_object_id) AS ParentSchema,
       OBJECT_NAME(fkc.referenced_object_id) AS ParentTable,
       COL_NAME(fkc.referenced_object_id, fkc.referenced_column_id) AS ParentColumn,
       fk.is_disabled
FROM sys.foreign_keys AS fk
JOIN sys.foreign_key_columns AS fkc
  ON fkc.constraint_object_id = fk.object_id
WHERE OBJECT_SCHEMA_NAME(fkc.parent_object_id) = 'dbo'
  AND OBJECT_NAME(fkc.parent_object_id) IN ('CTELEFONO', 'CTTELEFONO');
```

### Catálogo `CTTELEFONO`

```sql
SELECT c.column_id, c.name AS ColumnName, ty.name AS SqlType,
       c.max_length, c.precision, c.scale, c.is_nullable,
       dc.definition AS DefaultDefinition
FROM sys.tables AS t
JOIN sys.schemas AS s ON s.schema_id = t.schema_id
JOIN sys.columns AS c ON c.object_id = t.object_id
JOIN sys.types AS ty ON ty.user_type_id = c.user_type_id
LEFT JOIN sys.default_constraints AS dc
  ON dc.parent_object_id = c.object_id
 AND dc.parent_column_id = c.column_id
WHERE s.name = 'dbo' AND t.name = 'CTTELEFONO'
ORDER BY c.column_id;

SELECT TTL_FL_CVE, TTL_DS_TTELEFONO, TTL_FG_STATUS,
       TTL_FG_REGDEFAULT
FROM dbo.CTTELEFONO
WHERE TTL_FG_STATUS = 1
ORDER BY TTL_FL_CVE;
```

La segunda consulta sólo debe usarse si esas columnas son confirmadas por
`sys.columns`; no se asumirán nombres de vigencia o predeterminado.

### Consecutivo, estatus y duplicados sin PII

```sql
SELECT EMP_FL_CVE, CCS_DS_NOMTABLA, CCT_NO_CONSECUTIVO,
       CCT_DS_CAMPO, ID_FL_CVE
FROM dbo.CCATCONSEC
WHERE CCS_DS_NOMTABLA = 'CTELEFONO';

SELECT TFN_FG_STATUS, TFN_FG_REGDEFAULT, COUNT_BIG(*) AS Total
FROM dbo.CTELEFONO
GROUP BY TFN_FG_STATUS, TFN_FG_REGDEFAULT;

SELECT PNA_FL_PERSONA, COUNT_BIG(*) AS TotalPredeterminados
FROM dbo.CTELEFONO
WHERE TFN_FG_STATUS = 1 AND TFN_FG_REGDEFAULT = 1
GROUP BY PNA_FL_PERSONA
HAVING COUNT_BIG(*) > 1;
```

La última consulta sólo devuelve identificadores y conteos para localizar la
frecuencia de duplicados; sus resultados no deben copiarse a logs,
documentación o fixtures.

### Bitácora y actividad

```sql
SELECT a.ATV_FL_CVE, a.ATV_DS_DESCRIPCION, a.ATV_FG_BITACORA,
       a.ATV_FG_STATUS
FROM dbo.KACCION AS a
WHERE a.ATV_FL_CVE IN (14, 4, 5);
```

Las columnas anteriores son candidatas y deben verificarse con `sys.columns`
antes de ejecutarse. Si no coinciden, se debe consultar el esquema real y el
código de `Seguridad.Bitacora`; no se debe convertir `CommandArgumentControl`
en actividad por frecuencia o suposición.

## Pruebas previstas

- autorización `401`, `403`, antiforgery y tenant incorrecto;
- cliente/teléfono inexistente (`404`);
- catálogo inválido, longitudes y nulabilidad confirmadas (`400`);
- lista múltiple, vacía, activos e inactivos;
- primer teléfono predeterminado;
- múltiples predeterminados históricos con selección por menor ID;
- cambio de predeterminado y desactivación según la regla aprobada;
- concurrencia con `expectedModifiedAt`;
- consecutivo sólo en alta;
- bitácora transaccional y rollback;
- readiness recalculado después de cada mutación;
- SQL parametrizado, sin `SELECT *`, concatenación ni eliminación física;
- ausencia de números, lada, extensión y contacto en logs;
- UI responsive, panel accesible, permisos, doble envío y refresco.

## Bloqueos y decisiones pendientes

1. Confirmar valores y columnas reales de vigencia del catálogo
   `CTTELEFONO`, incluidos los tipos permitidos y la regla de celular/lada.
2. Confirmar si `TFN_FE_ULTMOD` permite concurrencia optimista y su precisión.
3. Aprobar la regla de desactivación del teléfono predeterminado y sus
   códigos `409`.
4. Identificar la actividad exacta de bitácora para alta, modificación,
   activación, desactivación y cambio de predeterminado.
5. Confirmar el formato de referencia de bitácora sin incluir PII.

Los bloqueos anteriores son documentales; el desacoplamiento aprobado ya
define que `DMO_FL_CVE` no participa en la administración de teléfonos.

## Actualización con metadatos autoritativos

Los resultados entregados de `pr_t` cierran la siguiente parte del modelo.

Para `DMO_FL_CVE`, la evidencia autoritativa confirma `int NOT NULL`, sin
default, sin FK y sin triggers. No existen registros históricos con valor `0`;
se identificaron 240 relaciones históricas cuyo domicilio ya no tiene
correspondencia. uCredit usará `0` únicamente en nuevas altas como convención
técnica de compatibilidad (“sin asociación de domicilio”), nunca como
identificador de un domicilio y sin actualizar masivamente registros previos.

### Esquema, claves e índices

| Columna | Tipo SQL | Nullable | Default | Uso propuesto |
|---|---|---:|---|---|
| `TFN_FL_CVE` | `int` | No | Ninguno | `phoneId`; PK y consecutivo |
| `TTL_FL_CVE` | `tinyint` | No | Ninguno | `phoneTypeCode` |
| `DMO_FL_CVE` | `int` | No en `pr_t` | Sin default | Alta uCredit: `0` técnico, sin asociación real; edición conserva el valor histórico |
| `TFN_CL_LARGA_DISTANCIA` | `varchar(10)` | Sí | Ninguno | `longDistanceCode` opcional |
| `TFN_CL_LADA` | `varchar(10)` | Sí | Ninguno | `areaCode` opcional |
| `TFN_CL_TELEFONO` | `varchar(20)` | Sí | Ninguno | `phoneNumber`; PII, requerido funcionalmente para alta |
| `TFN_CL_EXTENSION` | `varchar(10)` | Sí | Ninguno | `extension` opcional |
| `TFN_FG_STATUS` | `tinyint` | No | Ninguno | `statusCode`; `1` es activo |
| `TFN_DS_RAZON_INACTIVO` | `varchar(255)` | Sí | Ninguno | `inactiveReason` |
| `TFN_FG_REGDEFAULT` | `tinyint` | No | Ninguno | `isDefault`; `1` es predeterminado |
| `TFN_FE_ULTMOD` | `datetime(3)` | No | Ninguno | `modifiedAt` y candidato viable para concurrencia |
| `USR_CL_CVE` | `varchar(8)` | Sí | Ninguno | Actor Legacy interno; no DTO/log |
| `PNA_FL_PERSONA` | `int` | No | Ninguno | `personId` |
| `TFN_DS_CONTACTO` | `varchar(150)` | Sí | Ninguno | `contactName`; PII |

La PK física es el índice clustered único
`PK__CTELEFONO__51EF2864` sobre `TFN_FL_CVE`. Los índices no únicos
`XIF1CTELEFONO`, `XIF2CTELEFONO` y `XIF3CTELEFONO` cubren respectivamente
`TTL_FL_CVE`, `PNA_FL_PERSONA` y `DMO_FL_CVE`; son evidencia de acceso, no
prueba suficiente de una restricción FK.

Las consultas de `sys.foreign_keys` regresaron vacías. No existen FK físicas
confirmadas entre `CTELEFONO` y `CPERSONA`, `CDOMICILIO` o `CTTELEFONO`. La
única relación funcional del módulo es `PNA_FL_PERSONA`; el tipo se valida
 contra `CTTELEFONO`. `DMO_FL_CVE` queda fuera del contrato funcional y la
evidencia autoritativa de `pr_t` confirma que no acepta `NULL`. Las ediciones
no modifican la columna y las altas usan el valor técnico `0`, mediante
parámetro `DbType.Int32`. No se ejecutará una actualización masiva; los
valores históricos pueden permanecer
físicamente hasta que el teléfono sea editado.

### Tipos autorizados y consecutivo

Los tipos activos autorizados de `CTTELEFONO` son:

| Código | Descripción |
|---:|---|
| 1 | CASA |
| 2 | OFICINA |
| 3 | CELULAR |
| 4 | FAX |
| 5 | FISCAL |
| 6 | ACT.ECONOMICA |
| 10 | OTROS |
| 11 | OTROS CASA |
| 12 | OTROS OFICINA |
| 13 | OTROS CELULAR |

Todos los valores entregados tienen `TTL_FG_STATUS = 1` y
`TTL_FG_REGDEFAULT = 0`. No se deben aceptar claves fuera de esta lista ni
tipos que posteriormente aparezcan inactivos. No se debe tratar
`TTL_FG_REGDEFAULT` del catálogo como el predeterminado de un teléfono de
persona: ese campo pertenece al catálogo y la selección por persona usa
`CTELEFONO.TFN_FG_REGDEFAULT`.

`CCATCONSEC` confirma:

```text
EMP_FL_CVE = 0
CCS_DS_NOMTABLA = CTELEFONO
CCT_DS_CAMPO = TFN_FL_CVE
ID_FL_CVE = TFN_FL_CVE
```

El alta reservará el siguiente valor mediante el mecanismo transaccional de
`CCATCONSEC`. PUT, activación y desactivación no reservarán consecutivo.

### Estados, predeterminados y duplicados históricos

La distribución confirma que `TFN_FG_STATUS = 1` es el estado activo usado
por la consulta y readiness. También aparecen valores `0` y `2`; no se
convertirán automáticamente en estados modernos hasta confirmar el catálogo
funcional de estados. `TFN_FG_REGDEFAULT = 1` representa el predeterminado.

La distribución agregada indica:

- `726,506` personas tienen exactamente un teléfono activo predeterminado;
- `3` personas tienen dos teléfonos activos predeterminados;
- el total de personas con múltiples predeterminados es `3`;
- existen `1,958,788` teléfonos y ninguno tiene `TFN_FE_ULTMOD` nulo.

Así, los duplicados históricos son excepcionales pero reales. No se
normalizarán masivamente. La lectura seguirá seleccionando el menor
`TFN_FL_CVE` entre predeterminados activos; una nueva selección de
predeterminado podrá desmarcar los anteriores dentro de una única
transacción.

### Concurrencia y triggers

`TFN_FE_ULTMOD` es `datetime` con precisión/escala `23,3`, no nullable, sin
default y sin valores nulos en la distribución entregada. Es técnicamente
viable como token de concurrencia optimista:

- el DTO de PUT, activar y desactivar recibirá `expectedModifiedAt`;
- el UPDATE incluirá persona, teléfono y fecha esperada;
- cero filas afectadas producirá `409 phone_modified`;
- el valor nuevo será generado por el servidor dentro de la transacción.

La consulta de `sys.triggers` regresó vacía: no existen triggers en
`CTELEFONO`. Por tanto, todas las reglas de estado y predeterminado, la
auditoría y la actualización de `TFN_FE_ULTMOD` dependerán explícitamente de
la transacción de uCredit; no se delegarán a comportamiento implícito de
Legacy.

Consulta de evidencia utilizada, sólo de metadatos:

```sql
SELECT tr.name AS TriggerName, tr.is_disabled,
       OBJECT_DEFINITION(tr.object_id) AS Definition
FROM sys.triggers AS tr
WHERE tr.parent_id = OBJECT_ID('dbo.CTELEFONO');
```

El resultado vacío se conserva como evidencia de que no hay triggers en
`CTELEFONO`; no se copia lógica propietaria a esta documentación.

## Matriz definitiva propuesta para el DTO

Los DTOs HTTP no expondrán nombres de columnas Legacy ni valores de usuario
internos. La matriz funcional propuesta es:

| Propiedad DTO | Tipo | Requerida en alta | Máximo | Mapeo | Normalización |
|---|---|---:|---:|---|---|
| `phoneId` | `int` | No, sólo respuesta/ruta | — | `TFN_FL_CVE` | No aceptar en body de alta |
| `personId` | `int` | Ruta | — | `PNA_FL_PERSONA` | No aceptar en body |
| `phoneTypeCode` | `byte` | Sí | — | `TTL_FL_CVE` | Debe existir y estar activo en catálogo |
| `longDistanceCode` | `string?` | No | 10 | `TFN_CL_LARGA_DISTANCIA` | Trim; vacío se normaliza a null, sujeto a compatibilidad Legacy |
| `areaCode` | `string?` | No | 10 | `TFN_CL_LADA` | Trim; vacío se normaliza a null |
| `phoneNumber` | `string` | Sí funcionalmente | 20 | `TFN_CL_TELEFONO` | Trim; rechazar vacío y excedente |
| `extension` | `string?` | No | 10 | `TFN_CL_EXTENSION` | Trim; vacío se normaliza a null |
| `statusCode` | No exponer directamente | — | — | `TFN_FG_STATUS` | Backend controla alta/activar/desactivar |
| `inactiveReason` | `string?` | Condicional pendiente | 255 | `TFN_DS_RAZON_INACTIVO` | Trim; no registrar; regla de obligatoriedad pendiente |
| `isDefault` | `bool` | No, regla backend | — | `TFN_FG_REGDEFAULT` | Backend garantiza la regla de predeterminado |
| `modifiedAt` | `DateTime` | Respuesta | — | `TFN_FE_ULTMOD` | Token técnico ISO 8601 |
| `contactName` | `string?` | No | 150 | `TFN_DS_CONTACTO` | Trim; PII, sólo usuario autorizado |

La elección de `null` para campos opcionales vacíos evita inventar datos y
respeta la nulabilidad física. El Legacy observado envía cadenas vacías en
algunas altas; si se requiere compatibilidad exacta, esa diferencia deberá
aprobarse explícitamente antes de persistirla.

La UI puede mostrar lada, número, extensión y contacto únicamente a usuarios
con `customers.read` y nunca los almacenará en almacenamiento del navegador.
Los logs sólo tendrán etapa, excepción, número SQL cuando aplique y
correlation ID.

## Bitácora: trazabilidad de `CommandArgumentControl = 14`

La revisión de `su_MtoTelefono.aspx` confirma esta cadena:

```text
cmdGuardar.CommandArgumentControl = 14
  -> RevisaFirma(cmdGuardar, blnBit, intAcc, ...)
  -> ActualizaTelefono(..., intAcc, blnBit, ...)
  -> sd_clsSeguridad.Bitacora(intAcc, ...)
```

El argumento `14` es la autorización/control del botón. `ActualizaTelefono`
recibe `intAccion` y lo pasa a la bitácora después del commit Legacy, pero la
revisión disponible no demuestra que el número `14` sea el valor de
`ATV_FL_CVE`. La resolución final requiere seguir la implementación de
`RevisaFirma` y `sd_clsSeguridad.Bitacora` hasta la consulta de `KACCION`.
No se asignará `ATV_FL_CVE = 14` por inferencia.

La implementación moderna deberá decidir y documentar si conserva el
comportamiento Legacy de bitácora posterior al commit o usa la política
actual de uCredit de bitácora dentro de la transacción. En ambos casos la
referencia no contendrá números telefónicos, lada, extensión, contacto ni
payload.

## Consultas aún necesarias

Para cerrar los únicos bloqueos restantes se requieren, sin PII:

1. código exacto de `RevisaFirma` y `sd_clsSeguridad.Bitacora`, o una
   caracterización segura de `KACCION`, para resolver `ATV_FL_CVE`;
2. confirmación del formato de referencia de bitácora sin PII.

Con los metadatos y decisiones aprobadas ya no están bloqueados PK,
longitudes, nulabilidad, tipos activos, consecutivo, estados de mutación,
regla de predeterminado ni la viabilidad técnica de `TFN_FE_ULTMOD`. Las FK
físicas y los triggers no existen según las consultas vacías; por eso la
validación de persona, tipo y domicilio, las reglas, la auditoría y las
fechas quedan bajo control de la transacción de uCredit. Sólo permanece por
cerrar el detalle exacto del formato de referencia de bitácora.

## Decisiones aprobadas y plan final de implementación

Las siguientes reglas sustituyen cualquier propuesta preliminar anterior y
son el contrato funcional de la siguiente etapa.

### Estados administrables

| Código | Significado | Tratamiento |
|---:|---|---|
| `1` | Activo | Estado usado por altas y activación |
| `2` | Inactivo | Estado usado por desactivación |
| `0` | Histórico no administrable directamente | Sólo se muestra como `Inactivo heredado`; puede activarse, pero nunca se crea ni se asigna |

Las altas siempre escriben `TFN_FG_STATUS = 1`; activar escribe `1` y
desactivar escribe `2`. Ninguna operación nueva crea o asigna `0`. Los
registros con `0` no se eliminan físicamente y no se editan como si fueran
registros modernos; la única transición permitida para ellos es activarlos,
sujeta a las mismas validaciones de persona, tipo y concurrencia.

### Predeterminado

- El primer teléfono se crea activo y predeterminado.
- Las operaciones nuevas mantienen exactamente un predeterminado activo por
  persona.
- Elegir un teléfono como nuevo predeterminado desmarca todos los anteriores
  de esa persona dentro de la misma transacción.
- No se corregirán masivamente los tres casos históricos con más de un
  predeterminado activo.
- La lectura conserva la selección defensiva por menor `TFN_FL_CVE` y la UI
  marca cada fila que Legacy reporta como predeterminada.
- No se permite desactivar el predeterminado sin proporcionar otro teléfono
  activo como reemplazo. La respuesta es `409 phone_default_required`.
- La normalización de duplicados se limita a la persona que el usuario está
  modificando y ocurre al seleccionar un nuevo predeterminado.

La regla anterior no se implementará mediante una restricción física nueva.
El backend validará y normalizará dentro de la transacción, después de
validar que la persona y el teléfono pertenecen al despliegue Legacy actual.

### Concurrencia y bitácora aprobadas

`TFN_FE_ULTMOD` será el token de concurrencia para PUT, activación,
desactivación y cambio de predeterminado. El UPDATE incluirá persona,
identificador de teléfono y la fecha esperada; cero filas afectadas produce
`409 phone_modified`. La fecha nueva la asignará el servidor.

La actividad funcional queda fijada así:

| Operación | Actividad |
|---|---:|
| Alta | `4` |
| Modificación | `5` |
| Activación | `5` |
| Desactivación | `5` |
| Cambio de predeterminado | `5` |

`CommandArgumentControl = 14` sigue siendo únicamente el control de
autorización del botón Legacy. No representa `ATV_FL_CVE` ni se usará como
actividad de bitácora. La bitácora moderna se escribirá dentro de la misma
transacción y sólo incluirá etapa, actividad, actor interno y correlation ID;
no contendrá número, lada, extensión, contacto, domicilio ni payload.

### Evidencia de FK, triggers y distribución

Las consultas entregadas regresaron vacías tanto para `sys.foreign_keys`
como para `sys.triggers`. Por ello:

- no existen FK físicas confirmadas; persona y tipo de teléfono deben
  validarse en la aplicación antes de escribir;
- no existen triggers en `CTELEFONO`;
- todas las reglas de estado y predeterminado, la auditoría y la actualización
  de fechas dependen de la transacción de uCredit;
- no se agregará una FK ni se modificará el esquema Legacy.

La distribución recibida confirma que el estado activo es `1`, que existen
registros históricos `0` e inactivos `2`, que no hay fechas de modificación
nulas y que sólo tres personas presentan dos predeterminados activos. Estos
conteos no incluyen identificadores ni datos personales.

### DTOs definitivos

Los nombres HTTP serán funcionales y no expondrán nombres de columnas
Legacy:

| DTO | Propiedades | Reglas |
|---|---|---|
| `CustomerPhoneResponse` | `phoneId`, `personId`, `phoneTypeCode`, `longDistanceCode`, `areaCode`, `phoneNumber`, `extension`, `status`, `inactiveReason`, `isDefault`, `modifiedAt`, `contactName` | Sólo para usuarios autorizados; no expone asociación con domicilio |
| `CreateCustomerPhoneRequest` | `phoneTypeCode`, `longDistanceCode`, `areaCode`, `phoneNumber`, `extension`, `isDefault`, `contactName` | No recibe domicilio, estado, actor, consecutivo ni banderas Legacy |
| `UpdateCustomerPhoneRequest` | campos editables del alta, `expectedModifiedAt` | No recibe `phoneId`, estado Legacy ni `TFN_FG_REGDEFAULT` directo |
| `ChangeCustomerPhoneStatusRequest` | `expectedModifiedAt`, y `replacementPhoneId` al desactivar el predeterminado | El reemplazo es obligatorio en ese caso |

Los strings se recortan con `Trim()`. Los opcionales vacíos se normalizan a
`null`; los límites son 10 para larga distancia y lada, 20 para número, 10
para extensión, 255 para razón de inactividad y 150 para contacto. El
número es obligatorio funcionalmente en altas aunque la columna Legacy sea
nullable. El backend valida que no se excedan los límites físicos y usa
parámetros Dapper tipados con tamaño explícito.

### Desacoplamiento de domicilios

Los teléfonos pertenecen únicamente a la persona mediante `PNA_FL_PERSONA`.
La lectura no selecciona `DMO_FL_CVE` ni hace `JOIN` con `CDOMICILIO`; las
mutaciones no consultan ni exigen domicilios. Las altas escriben `0` mediante
un parámetro entero tipado; `0` es una convención técnica de compatibilidad de
uCredit y no representa un domicilio real. La edición no modifica
`DMO_FL_CVE`, incluyendo la edición de un registro histórico previamente
asociado. La UI no muestra selector,
texto ni identificador de domicilio en el panel telefónico y los DTOs HTTP
no aceptan ni devuelven `addressId`.

### Endpoints finales

```text
GET  /api/v1/customers/{personId}/phones
POST /api/v1/customers/{personId}/phones
PUT  /api/v1/customers/{personId}/phones/{phoneId}
POST /api/v1/customers/{personId}/phones/{phoneId}/activate
POST /api/v1/customers/{personId}/phones/{phoneId}/deactivate
```

GET requiere `customers.read`. Las mutaciones requieren autenticación,
`customers.write`, antiforgery, tenant seleccionado coincidente con
`Deployment__TenantCode`, membresía activa con `LegacyUserCode`, entorno
Development, conexión Legacy de escritura y guardas explícitas de `pr_t`.
Customers sigue delimitado por despliegue; no se usará `AllowedCompanyIds`.

Las respuestas serán `401` para anónimo, `403` para permiso o tenant
inválido, `404` para persona/teléfono inexistente, `400` para
payload o catálogo inválido, `409 phone_default_required` para desactivar
sin reemplazo, `409 phone_modified` para concurrencia y `503` únicamente
para escritura Legacy no configurada o base distinta de `pr_t`.

### Transacción, normalización y reversa

Cada mutación abrirá una transacción Dapper después de las validaciones de
seguridad y catálogo:

1. validar persona, teléfono, tipo, actor y configuración;
2. reservar `TFN_FL_CVE` sólo en alta;
3. bloquear la fila objetivo y comprobar `TFN_FE_ULTMOD`;
4. comprobar el reemplazo cuando se desactive el predeterminado;
5. desmarcar predeterminados anteriores si se selecciona uno nuevo;
6. escribir con parámetros tipados y estado `1` o `2`;
7. registrar actividad 4 o 5 en `KBITACORA`;
8. confirmar; ante cualquier error, rollback completo.

Activar un histórico `0` lo convierte a `1`; ninguna ruta lo convierte a
`2` sin la operación explícita de desactivar. No se harán cargas ni
actualizaciones masivas de duplicados.

### UI y seguridad de PII

El expediente mostrará una lista responsive de teléfonos activos, inactivos
e históricos heredados, con distintivos de estado y predeterminado. El panel
lateral de alta/edición sólo mostrará tipos activos del catálogo; ofrecerá
activar, desactivar y elegir predeterminado según permisos. Desactivar el
predeterminado exigirá seleccionar un reemplazo activo. La UI bloqueará el
doble envío, anunciará guardando/éxito/error y refrescará teléfonos y
readiness después de cada operación.

Ningún teléfono, lada, extensión o contacto se guardará en
`localStorage`/`sessionStorage`, URL, logs, excepciones, fixtures ni
mensajes de error. Los logs sólo contendrán etapa, tipo de excepción y
correlation ID.

### Pruebas de la implementación

La siguiente fase deberá cubrir:

- `401`, `403`, antiforgery, tenant incorrecto, `404`, `400` y `503`;
- listado múltiple, lista vacía, estados `1`, `2` y `0` como histórico;
- alta con estado `1`, consecutivo y primer predeterminado;
- activación de histórico, desactivación a `2` y rechazo del `0` en altas;
- cambio de predeterminado y desmarcado transaccional de anteriores;
- `phone_default_required` y `phone_modified`;
- actividad 4/5 y bitácora dentro de la transacción;
- rollback ante fallos de catálogo, concurrencia o auditoría;
- SQL parametrizado, tamaños Dapper y ausencia de escrituras no autorizadas;
- ausencia de números telefónicos y demás PII en logs, respuestas no
  autorizadas, fixtures y almacenamiento del navegador;
- UI responsive, permisos, estados, replacement requerido y doble envío.

El plan queda listo para implementación. Antes de la primera escritura sólo
debe fijarse el formato exacto de referencia de bitácora; no se inventarán
relaciones físicas, triggers ni datos de auditoría, y no se ejecutarán
escrituras para resolver esa decisión.
