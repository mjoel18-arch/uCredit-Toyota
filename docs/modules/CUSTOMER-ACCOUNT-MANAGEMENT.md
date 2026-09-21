# Administración múltiple de cuentas

## Estado de esta etapa

Esta entrega es únicamente de análisis. No agrega endpoints, comandos,
escrituras Legacy, migraciones ni cambios de esquema.

La regla ya aprobada para readiness es mínima: una persona activa cumple el
requisito de cuenta cuando existe al menos una fila de `dbo.CPCUENTA` con
`PCT_FG_STATUS = 1`. Readiness no exige una cuenta predeterminada ni expone
número de cuenta o CLABE.

La futura administración de cuentas requiere reglas adicionales que aún no
están cerradas. En particular, la nulabilidad física de número y CLABE no
determina por sí sola si son obligatorios funcionalmente para una cuenta que
se vaya a capturar.

## Trazabilidad Legacy revisada

La pantalla revisada es:

```text
su_ConCtasPagoPersona.aspx
  -> cmdGuardar_Click
  -> sn_clsPersona.ActualizaCuentaPago
  -> sd_clsPersona.ActualizaCuentaPago
```

La lectura de la ficha utiliza `ObtenCuentaPago` y
`ObtenCuentaPagoContratoDS`. La pantalla carga bancos con `ObtenBanco`,
monedas mediante `ObtenParametro(4, ...)`, tipos de cuenta mediante
`ObtenParametro(71, ...)` y estatus mediante `ObtenParametroStatus(...)`.

En el alta, Legacy reserva el consecutivo de `CPCUENTA` dentro de una
transacción `Serializable`, inserta la cuenta y confirma antes de ejecutar la
bitácora posterior. En la modificación actualiza por `PCT_FL_CVE`, asigna
`PCT_FE_ULTMOD = GETDATE()` y también confirma antes de la bitácora. El
adaptador nuevo no copiará SQL concatenado ni expondrá esta implementación.

La página exige actualmente sucursal, número de cuenta, CLABE de 18 dígitos
numéricos, banco, moneda, tipo de cuenta y estatus. Esa es evidencia del
comportamiento de esa pantalla, no una aprobación de que la API moderna deba
exigir o devolver esos valores sin cerrar primero la regla funcional y de
protección de datos.

## Matriz confirmada de `CPCUENTA`

| Columna | Tipo SQL | Nullable | Default confirmado | Uso | Sensibilidad |
|---|---|---:|---|---|---|
| `PCT_FL_CVE` | `int` | No | Ninguno | Identificador físico y consecutivo | No sensible |
| `BCO_FL_CVE` | `smallint` | No | Ninguno | Banco | No sensible por sí solo |
| `PCT_NO_SUCURSAL` | `smallint` | No | Ninguno | Sucursal numérica capturada por Legacy | No sensible por sí solo |
| `PCT_NO_CUENTA` | `varchar(20)` | Sí | Ninguno | Número de cuenta | Financiera sensible |
| `PCT_NO_CLABE` | `varchar(20)` | Sí | Ninguno | CLABE | Financiera sensible |
| `PCT_CL_MONEDA` | `tinyint` | No | Ninguno | Clave de moneda | Código funcional |
| `PCT_CL_TCUENTA` | `tinyint` | No | Ninguno | Tipo de cuenta | Código funcional |
| `PCT_FG_STATUS` | `tinyint` | No | Ninguno | Estado de la cuenta | Código funcional |
| `PCT_FE_ULTMOD` | `datetime` | No | Ninguno | Fecha de modificación | Token técnico |
| `USR_CL_CVE` | `varchar(8)` | Sí | Ninguno | Usuario Legacy | Interno; nunca DTO/log |
| `PNA_FL_PERSONA` | `int` | No | Ninguno | Relación con la persona | Identificador de dominio |
| `PCT_DS_INST_DEPOSITO` | `varchar(255)` | Sí | Ninguno | Instrucciones de depósito | Puede contener información sensible |
| `PCT_CL_MPAGO` | `int` | Sí | Ninguno | Medio de pago | Alta `NULL`; edición conserva histórico; no se expone |

La PK física confirmada es el índice clustered único
`PK__CPCUENTA__318258D2` sobre `PCT_FL_CVE`. La evidencia no la marca como
identity. `CCATCONSEC` confirma el consecutivo de `CPCUENTA` para la empresa
`0`, con campo e identificador `PCT_FL_CVE`. El alta debe reservarlo con el
mecanismo transaccional de Legacy; una modificación no debe reservar un nuevo
identificador.

El índice no único `PERSONA` sobre `PNA_FL_PERSONA` confirma un patrón de
consulta, no una restricción de unicidad. La evidencia disponible no confirmó
FK físicas para `CPCUENTA`; por tanto, la relación con `CPERSONA` es lógica y
debe validarse en la aplicación antes de cualquier escritura futura.

## Relaciones y catálogos

| Dato | Evidencia disponible | Estado |
|---|---|---|
| Persona | `CPCUENTA.PNA_FL_PERSONA` se compara con `CPERSONA.PNA_FL_PERSONA` | Relación lógica; no hay FK física |
| Banco | La pantalla carga `CBANCO` y Legacy relaciona `BCO_FL_CVE` | Estado activo y clasificación real validados por aplicación |
| Moneda | La pantalla carga `CPARAMETRO` con catálogo `4` | Valores vigentes confirmados: 1, 2 y 3 |
| Tipo de cuenta | La pantalla carga `CPARAMETRO` con catálogo `71` | Valores vigentes confirmados: 1, 2 y 3 |
| Medio de pago | `PCT_CL_MPAGO` es nullable y no aparece como selección | Alta `NULL`; edición conserva histórico; no se expone |
| País | La UI puede iniciar con México (`PAI_FL_CVE = 1`); el backend valida existencia en `CPAIS` | No restringir exclusivamente a México |
| Sucursal | `PCT_NO_SUCURSAL` es `smallint NOT NULL`; no se confirmó tabla `CSUCURSAL` | Valor numérico obligatorio; no es texto ni catálogo |
| `CBCO_CTAS` | Contiene cuentas bancarias operativas de empresa, con `EMP_FL_CVE` | No asumir que representa cuentas del cliente |

No se deben interpretar `CBCO_CTAS`, `CPAIS_CUENTA` o los códigos de
`CPARAMETRO` como relaciones de `CPCUENTA` sin confirmar sus columnas y
reglas mediante metadatos y código Legacy.

## Estados, formatos y validación

La regla aprobada para readiness usa `PCT_FG_STATUS = 1` como cuenta activa.
La distribución recibida también muestra `2`, pero el significado completo de
los estados administrables y las transiciones de activación/desactivación no
se ha confirmado como catálogo funcional para la API.

La columna `PCT_NO_CUENTA` admite hasta 20 caracteres y `PCT_NO_CLABE` hasta
20 caracteres físicamente. La pantalla Legacy limita el número de cuenta a
20 y exige una CLABE numérica de 18 dígitos. No se encontró en el flujo
revisado un algoritmo de validación de dígito verificador de CLABE. El
procedimiento `SpDigitoVerificadorXPer` aparece preparado en código, pero su
ejecución está comentada; no se puede tratar como validación vigente.

La pantalla comprueba que el número de cuenta activo no esté repetido,
excluyendo la cuenta que se edita. También existe un método Legacy para
comprobar CLABE, pero la comprobación correspondiente está comentada en el
evento de guardado. La API impondrá la regla aprobada de duplicidad por
persona y sólo para filas activas, sin unicidad global.

No existe una columna de cuenta predeterminada confirmada en `CPCUENTA` y no
se encontró una tabla de relación predeterminada en la evidencia revisada.
No se debe inventar una cuenta principal.

## Sensibilidad y contrato de consulta

La API futura podrá exponer únicamente un DTO equivalente a:

```text
accountId
bankId
bankName
branch
currencyCode
accountTypeCode
paymentMethodCode
status
maskedAccountNumber
maskedClabe
modifiedAt
```

Reglas obligatorias:

- nunca seleccionar ni devolver el número completo o la CLABE completa;
- enmascarar dejando, como máximo, la terminación de cuatro caracteres;
- no registrar cuenta, CLABE, sucursal, instrucciones de depósito ni payload;
- no incluirlos en mensajes de error, URLs, fixtures, `localStorage` o
  `sessionStorage`;
- no volver a enviar el valor existente al abrir una edición;
- utilizar datos sintéticos no financieros en pruebas.

La lectura de una cuenta existente debe devolver sólo la representación
enmascarada. El adaptador Dapper deberá materializar una fila de
infraestructura y construir explícitamente el DTO seguro; no debe exponer un
modelo Legacy con secretos financieros.

## Edición y reemplazo de datos sensibles

La pantalla Legacy vuelve a cargar y envía el número y la CLABE completos al
editar. Ese comportamiento no es compatible por sí solo con la regla de
seguridad de uCredit.

La propuesta moderna es un request con presencia distinguible por propiedad:

- propiedad ausente: conservar el valor existente;
- propiedad presente con cadena vacía: reemplazar por vacío o limpiar sólo
  si la regla funcional y la nulabilidad de Legacy lo autorizan;
- propiedad presente con valor nuevo: validar y reemplazar;
- la respuesta de la API nunca permite recuperar el valor anterior completo.

Esta semántica requiere confirmación explícita antes de implementarse. No se
debe aproximar con un DTO que convierta indistintamente ausencia, `null` y
cadena vacía.

## Endpoints preliminares

```text
GET  /api/v1/customers/{personId}/accounts
POST /api/v1/customers/{personId}/accounts
PUT  /api/v1/customers/{personId}/accounts/{accountId}
POST /api/v1/customers/{personId}/accounts/{accountId}/activate
POST /api/v1/customers/{personId}/accounts/{accountId}/deactivate
```

GET requiere `customers.read`. Las mutaciones requerirán `customers.write`,
antiforgery, tenant seleccionado coincidente con `Deployment__TenantCode`,
`LegacyUserCode` de la membresía activa, entorno Development, conexión Legacy
de escritura y guardas efectivas de `DB_NAME() = pr_t`.

Customers seguirá delimitado por tenant/despliegue. No se utilizará
`AllowedCompanyIds`, porque no existe una relación confirmada entre
`CPERSONA` y empresa para este vertical.

Respuestas previstas:

- `401` anónimo;
- `403` sin permiso o tenant inválido;
- `404` persona o cuenta inexistente;
- `400` payload, catálogo o formato inválido;
- `409` conflicto de concurrencia o regla de duplicidad, sólo después de
  confirmar el contrato funcional;
- `503` únicamente cuando la escritura Legacy no esté configurada o la base
  efectiva no sea `pr_t`.

## Transacciones, concurrencia y auditoría

El Legacy usa una transacción `Serializable` para insertar o actualizar la
fila de `CPCUENTA`. En el alta obtiene `PCT_FL_CVE` mediante `CCATCONSEC`; en
la edición conserva el identificador. El flujo posterior intenta calcular
una clave CIEC, pero la ejecución del procedimiento está comentada en la
fuente revisada y no debe ser inventada en uCredit.

`PCT_FE_ULTMOD` es `datetime NOT NULL` y se actualiza con `GETDATE()` en la
modificación. Es un candidato viable para concurrencia optimista moderna,
pero el Legacy observado no condiciona su `UPDATE` por la fecha; la API no
debe presentarlo como regla confirmada hasta aprobar el contrato de
concurrencia.

La pantalla usa `CommandArgumentControl = 23` para agregar y `24` para
guardar. En `sd_clsPersona.ActualizaCuentaPago`, la inserción llama a la
bitácora con el código interno `25`, mientras que la modificación pasa
`intAccion`. Estos números no deben convertirse automáticamente en
`ATV_FL_CVE`; es necesario revisar `KACCION`, `RevisaFirma` y
`sd_clsSeguridad.Bitacora` para identificar la actividad efectiva y el
formato de `BIT_DS_REFERENCIA`.

Además, el Legacy registra la bitácora después de confirmar la transacción de
la cuenta. La política moderna de uCredit exige que auditoría y escritura
sean una unidad transaccional; esa diferencia debe documentarse y aprobarse
antes de implementar.

No hay evidencia autoritativa en esta etapa sobre triggers de `CPCUENTA`,
FK físicas o una restricción de unicidad financiera. Deben confirmarse con
`sys.triggers`, `sys.foreign_keys` e índices antes de escribir.

## Consultas de sólo lectura pendientes

Estas consultas están preparadas para una base autorizada y no deben
ejecutarse como parte de esta etapa. No seleccionan números de cuenta, CLABE
ni PII.

### Esquema, defaults y nulabilidad

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
WHERE s.name = 'dbo' AND t.name IN
  ('CPCUENTA', 'CBANCO', 'CBCO_CTAS', 'CPAIS_CUENTA')
ORDER BY t.name, c.column_id;
```

### PK, índices, FK y triggers

```sql
SELECT s.name AS SchemaName, t.name AS TableName, i.name AS IndexName,
       i.type_desc, i.is_unique, i.is_primary_key, i.is_unique_constraint,
       ic.key_ordinal, c.name AS ColumnName, ic.is_included_column
FROM sys.tables AS t
JOIN sys.schemas AS s ON s.schema_id = t.schema_id
JOIN sys.indexes AS i ON i.object_id = t.object_id
JOIN sys.index_columns AS ic
  ON ic.object_id = i.object_id AND ic.index_id = i.index_id
JOIN sys.columns AS c
  ON c.object_id = ic.object_id AND c.column_id = ic.column_id
WHERE s.name = 'dbo'
  AND t.name IN ('CPCUENTA', 'CBANCO', 'CBCO_CTAS', 'CPAIS_CUENTA')
ORDER BY t.name, i.name, ic.key_ordinal, ic.index_column_id;

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
  AND OBJECT_NAME(fkc.parent_object_id) IN
      ('CPCUENTA', 'CBANCO', 'CBCO_CTAS', 'CPAIS_CUENTA');

SELECT tr.name AS TriggerName, tr.is_disabled
FROM sys.triggers AS tr
WHERE tr.parent_id IN
  (OBJECT_ID('dbo.CPCUENTA'), OBJECT_ID('dbo.CBANCO'),
   OBJECT_ID('dbo.CBCO_CTAS'), OBJECT_ID('dbo.CPAIS_CUENTA'));
```

### Catálogos y consecutivo

```sql
SELECT EMP_FL_CVE, CCS_DS_NOMTABLA, CCT_NO_CONSECUTIVO,
       CCT_DS_CAMPO, ID_FL_CVE
FROM dbo.CCATCONSEC
WHERE CCS_DS_NOMTABLA = 'CPCUENTA';

SELECT PAR_FL_CVE, PAR_CL_VALOR, PAR_DS_DESCRIPCION,
       PAR_FG_STATUS, PAR_FG_REGDEFAULT
FROM dbo.CPARAMETRO
WHERE PAR_FL_CVE IN (4, 71)
ORDER BY PAR_FL_CVE, PAR_CL_VALOR;
```

El catálogo de medio de pago no debe suponerse a partir de la columna
`PCT_CL_MPAGO`; primero debe localizarse su `PAR_FL_CVE` o tabla real con
metadatos y código Legacy.

### Distribuciones sin valores financieros

```sql
SELECT PCT_FG_STATUS, PCT_CL_MONEDA, PCT_CL_TCUENTA,
       PCT_CL_MPAGO, COUNT_BIG(*) AS Total
FROM dbo.CPCUENTA
GROUP BY PCT_FG_STATUS, PCT_CL_MONEDA, PCT_CL_TCUENTA, PCT_CL_MPAGO;

SELECT
  SUM(CASE WHEN PCT_NO_CUENTA IS NULL THEN 1 ELSE 0 END) AS NullAccountCount,
  SUM(CASE WHEN PCT_NO_CLABE IS NULL THEN 1 ELSE 0 END) AS NullClabeCount,
  COUNT_BIG(*) AS Total
FROM dbo.CPCUENTA;
```

La segunda consulta sólo devuelve completitud agregada. No se deben
seleccionar ni imprimir los valores de las columnas sensibles.

## UX y protección de datos

La ficha mostrará cuentas activas e inactivas con banco, sucursal, moneda,
tipo, medio de pago y estado. Número de cuenta y CLABE sólo se representarían
enmascarados, como una terminación limitada, después de autorización.

El panel de alta/edición no debe precargar valores financieros completos. La
UI bloqueará doble envío, anunciará guardando/éxito/error, pedirá confirmación
antes de desactivar y refrescará cuentas y readiness tras una mutación. No
almacenará valores en `localStorage` o `sessionStorage` ni los pondrá en la
URL.

Las acciones de escritura sólo se mostrarán con `customers.write`; la
autoridad de validación y elegibilidad permanecerá en backend.

## Pruebas propuestas

Antes de habilitar escrituras deben existir pruebas para:

- `401`, `403`, antiforgery, tenant incorrecto y `404`;
- lectura con lista vacía, múltiple, activa e inactiva;
- DTO sin número completo, CLABE completa, instrucciones de depósito ni
  payload sensible;
- enmascarado determinista y ausencia de valores completos en logs, errores,
  URL, almacenamiento del navegador y fixtures;
- catálogos de banco, moneda, tipo y medio de pago vigentes;
- formato y validación de sucursal, cuenta y CLABE;
- cuenta/CLABE ausente, vacía y reemplazo con semántica explícita;
- consecutivo sólo en alta;
- activación y desactivación sin borrado físico;
- concurrencia con `PCT_FE_ULTMOD`, si se aprueba;
- bitácora, rollback y fallo de auditoría;
- escritura limitada a Development y `pr_t`;
- actualización de readiness después de cada mutación;
- doble envío y refresco frontend.

## Bloqueos y decisiones pendientes

1. Confirmar FK, triggers, defaults, restricciones únicas y todos los índices
   de `CPCUENTA` y tablas relacionadas.
2. Obtener los valores y vigencia de catálogos de moneda, tipo de cuenta,
   medio de pago y estatus; el código Legacy confirma los catálogos `4` y
   `71`, no sus valores funcionales completos.
3. Aprobar si cuenta, CLABE o ambas son obligatorias en la API moderna.
4. Confirmar algoritmo de CLABE; el código revisado sólo exige 18 caracteres
   numéricos y no ejecuta el procedimiento de dígito verificador.
5. Confirmar duplicidad y alcance de unicidad para cuenta y CLABE.
6. Confirmar estados administrables y reglas de activación/desactivación,
   incluida la protección de cuentas asociadas a domiciliación.
7. Aprobar `PCT_FE_ULTMOD` como token de concurrencia moderna.
8. Identificar la actividad real de bitácora, el tratamiento de códigos
   internos `23`, `24`, `25` y el formato de referencia sin PII.
9. Aprobar la semántica triestado de edición: campo ausente, `null`, cadena
   vacía y valor nuevo.

No se debe implementar código productivo ni ejecutar SQL, POST, migraciones,
IdentityAdmin, commit o push hasta cerrar estos bloqueos.

## Actualización autoritativa de catálogos y reglas aprobadas

Los siguientes resultados de metadatos y distribuciones agregadas se
incorporan como evidencia de `pr_t`. No contienen números de cuenta, CLABE,
personas ni otros valores financieros.

### Catálogo de moneda: `CPARAMETRO`, catálogo 4

| Código | Descripción | Vigente | Predeterminado |
|---:|---|---:|---:|
| `1` | PESOS | Sí | Sí |
| `2` | DOLARES | Sí | No |
| `3` | UDIS | Sí | No |

El registro `0` es la fila descriptiva del catálogo y no es una opción
seleccionable. La API sólo podrá aceptar códigos vigentes de este catálogo.

### Catálogo de tipo de cuenta: `CPARAMETRO`, catálogo 71

| Código | Descripción | Vigente | Predeterminado |
|---:|---|---:|---:|
| `1` | CHEQUES | Sí | Sí |
| `2` | TARJETA DE CREDITO | Sí | No |
| `3` | TARJETA DE DEBITO | Sí | No |

El registro `0` es la fila descriptiva del catálogo y no es una opción
seleccionable. No se aceptarán valores fuera de las opciones vigentes.

### Esquema adicional de `CBANCO`

La tabla usa `BCO_FL_CVE smallint NOT NULL` como identificador. `BCO_DS_NOMBRE`
es `varchar(50) NULL`; `BCO_DS_CVEBANXICO` es `varchar(50) NOT NULL`; los
campos de dirección, contacto y recepción son opcionales con las longitudes
físicas documentadas en la evidencia; `BCO_FG_REGDEFAULT tinyint NOT NULL`,
`BCO_FG_STATUS tinyint NOT NULL`, `BCO_FE_ULTMOD datetime NOT NULL`,
`USR_CL_CVE varchar(8) NULL` y `BCO_FG_REAL tinyint NOT NULL` completan el
esquema confirmado.

La existencia de `BCO_FG_STATUS` confirma que el banco debe validarse contra
un registro vigente antes de una futura escritura, pero no se recibió una
distribución de bancos ni la regla que define qué registros son utilizables
por clientes. La evidencia de `BCO_FG_STATUS` y `BCO_FG_REAL` permite cerrar
el catálogo de clientes como bancos activos y reales; los nombres se obtienen
dinámicamente del catálogo Legacy y no se codifican en el repositorio.

### Estado y completitud agregada de `CPCUENTA`

La distribución autoritativa contiene:

| Estado | Total |
|---:|---:|
| `1` | 298,090 |
| `2` | 199,130 |

El estado `1` es el estado activo aprobado para readiness y para nuevas
cuentas. El estado `2` se trata como no activo para la consulta; las
transiciones modernas de activación y desactivación quedan sujetas al
contrato de escritura aprobado más adelante.

En la distribución entregada, todas las filas tienen número de cuenta; las
filas activas tienen 298,090 con número. Hay 298,082 filas activas con CLABE
y 298,082 con ambas piezas. Esto no cambia la decisión funcional: cuenta y
CLABE serán obligatorias para nuevas cuentas aunque ambas columnas sean
nullable en SQL. Los conteos sólo son evidencia auxiliar y no se usarán para
inferir una regla de validación de datos existentes.

Las combinaciones observadas de códigos son:

| Moneda | Tipo de cuenta | Medio de pago | Estado | Total |
|---:|---:|---:|---:|---:|
| `1` | `1` | `NULL` | `1` | 297,997 |
| `1` | `1` | `NULL` | `2` | 199,130 |
| `1` | `2` | `NULL` | `1` | 2 |
| `1` | `3` | `NULL` | `1` | 91 |

La frecuencia no convierte `PCT_CL_MPAGO` en un valor por defecto. El campo
es nullable y no se confirmó su catálogo ni una regla funcional para el alta.
No se asignará un valor inventado.

### Duplicados históricos

La evidencia agregada reporta 530 números de cuenta duplicados y 1,514 CLABEs
duplicadas. No identifica valores ni personas, y no permite deducir el
alcance de la duplicidad: podría ser por instalación, persona, banco, estado
o una combinación de esos campos.

Por ello:

- no se corregirán duplicados masivamente;
- no se rechazará automáticamente una cuenta existente sin confirmar la
  regla funcional y su alcance;
- no se expondrán duplicados en GET ni en logs;
- la regla moderna de duplicidad queda pendiente de aprobación explícita.

La nueva distribución agrega el alcance por persona y estado activo:

| Métrica | Total |
|---|---:|
| Personas con cuenta activa duplicada | 6,419 |
| Personas con CLABE activa duplicada | 631 |

Estos conteos no prueban unicidad global ni identifican los registros. Los
índices confirmados siguen siendo `CLAVEBANCO` sobre `BCO_FL_CVE`, `PERSONA`
sobre `PNA_FL_PERSONA` y la PK sobre `PCT_FL_CVE`; ninguno es único salvo la
PK.

La decisión propuesta para la API moderna es:

- no aplicar unicidad global;
- rechazar una cuenta o CLABE duplicada activa dentro de la misma persona;
- excluir el propio `PCT_FL_CVE` al editar;
- no corregir registros históricos;
- responder `409 account_duplicate`.

Esta decisión queda marcada como propuesta funcional hasta su aprobación
final. La detección futura deberá comparar sólo filas activas de la misma
persona y ejecutarse dentro de la transacción, sin devolver ni registrar los
valores comparados.

### Triggers

La consulta reportó que no existen triggers. Se documenta como evidencia de
`CPCUENTA` sin triggers confirmados. En consecuencia, una futura operación
moderna deberá mantener explícitamente validación, concurrencia, auditoría,
estado y rollback dentro de la transacción de uCredit; no se dependerá de
efectos implícitos de SQL Server.

## Contrato funcional aprobado para la siguiente implementación

Estas reglas sustituyen las propuestas preliminares anteriores:

- las cuentas nuevas deben incluir número de cuenta y CLABE;
- la CLABE debe tener exactamente 18 dígitos;
- no se calculará ni validará el dígito verificador hasta contar con
  evidencia autoritativa del algoritmo Legacy;
- GET sólo devuelve terminaciones enmascaradas, como máximo los últimos
  cuatro caracteres;
- nunca se devuelve ni registra el valor completo;
- en PUT, una propiedad ausente o `null` conserva el valor existente;
- un valor no vacío reemplaza el valor existente;
- una cadena vacía o compuesta sólo por espacios produce `400`;
- no se permite borrar cuenta ni CLABE;
- `PCT_FE_ULTMOD` será el token de concurrencia;
- un conflicto devuelve `409 account_modified`;
- alta usa actividad `4`;
- modificación, activación y desactivación usan actividad `5`;
- la bitácora se escribe dentro de la misma transacción;
- nunca se eliminan físicamente filas de `CPCUENTA`.

La distinción entre propiedad ausente, `null`, cadena vacía y valor nuevo es
obligatoria en el request. El DTO no deberá convertir esos casos a un único
valor por defecto. La respuesta de una edición nunca contendrá el secreto
financiero existente.

La concurrencia se implementará con una condición que incluya persona,
identificador de cuenta y `PCT_FE_ULTMOD`; cero filas afectadas producirá
`409 account_modified`. Esta regla ya está aprobada para uCredit, aunque el
Legacy original actualiza por identificador y sólo asigna `GETDATE()` sin
condición de fecha.

## Pendientes que permanecen abiertos

La evidencia recibida todavía no determina inequívocamente:

1. cuáles bancos están activos según `BCO_FG_STATUS` ni si
   `BCO_FG_REAL` restringe bancos de clientes;
2. el catálogo y la regla funcional de `PCT_CL_MPAGO`;
3. la relación funcional de país y el uso de `CPAIS_CUENTA`;
4. el catálogo y valores de `PCT_CL_MPAGO`. Los catálogos 29 y 44 recibidos
   describen forma de pago del seguro y del contrato, respectivamente; no se
   interpretan como medio de pago de `CPCUENTA`;
5. el alcance final de unicidad para números de cuenta y CLABEs. La propuesta
   es por persona y sólo para filas activas, con `409 account_duplicate`;
6. la actividad exacta de `KACCION` correspondiente a alta, modificación,
   activación y desactivación, y el formato aprobado de referencia sin PII;
7. la existencia o ausencia de FK físicas, índices únicos adicionales y
   defaults en la ejecución completa de las consultas de metadatos de
   `CPCUENTA` y tablas relacionadas. La evidencia recibida ya confirma que
   no existen triggers, no hay llave foránea y todos los defaults de
   `CPCUENTA` son `NULL`.

La evidencia autoritativa confirma que `PCT_NO_SUCURSAL` se modelará como
`short`/`smallint`, es obligatorio, no conserva ceros iniciales y no se
tratará como texto. La pantalla Legacy lo captura numéricamente y no se
confirmó una relación con `CSUCURSAL`; la API tampoco creará esa relación.
La distribución agregada confirma valores dentro del dominio observado:

| Mínimo | Máximo | Registros con cero | Negativos | Total |
|---:|---:|---:|---:|---:|
| 0 | 9,981 | 471,414 | 0 | 497,220 |

El rango observado no sustituye la validación del rango de `short`, pero
confirma que no hay negativos ni valores fuera de ese dominio en la muestra
agregada autorizada.

Para país, la UI podrá proponer México (`PAI_FL_CVE = 1`) como valor inicial.
El backend validará la existencia del identificador en `CPAIS`, sin aplicar
un estatus que no fue confirmado y sin restringir la selección exclusivamente
a México. El repositorio no hardcodeará `1` como única opción ni aceptará el
valor de la UI sin esa validación.

Para banco, el catálogo activo será obligatorio: la UI enviará únicamente el
identificador, y la respuesta autorizada podrá devolver identificador y
nombre. Aún falta la distribución o regla que identifique cuáles registros
de `CBANCO` están activos y cuáles son utilizables por clientes.

No se implementará código productivo hasta presentar la matriz final de estos
puntos y aprobar las decisiones pendientes.

## Fuentes Legacy revisadas

La revisión fue de sólo lectura sobre la fuente Legacy local. No se copiaron
fuentes Legacy al repositorio uCredit.

- `Sitio Web/Migrado/su_ConCtasPagoPersona.aspx` y `.aspx.vb`:
  controles, límites, catálogos, duplicidad y evento de guardado;
- `snLsenet/sn_clsPersona.vb`: delegación de lectura, validación y alta/
  modificación;
- `sdLsenet/sd_clsPersona.vb`: transacción `Serializable`, inserción,
  actualización, consecutivo y bitácora;
- `Proleasenet.Clases/Pcuenta.vb`: columnas y SQL generador del modelo
  Legacy, utilizado sólo como evidencia histórica.

La documentación resume reglas y referencias; no reproduce SQL ni código
propietario completo.

## Cierre de evidencia y matriz final

Esta sección sustituye las propuestas preliminares anteriores cuando exista
alguna diferencia. La implementación sigue bloqueada hasta resolver los
puntos indicados al final.

### Bancos

La fuente Legacy `sd_clsCatalogo.ObtenBanco` filtra la lista de
`su_ConCtasPagoPersona` por `BCO_FG_STATUS = 1` y relaciona ese estado con el
catálogo de estatus. No utiliza `BCO_FG_REAL` para cargar el catálogo de
mantenimiento.

La evidencia de código sí muestra `BCO_FG_REAL = 1` en consultas de selección
de bancos reales para operaciones de contrato/pago. Por tanto:

- `BCO_FG_STATUS` determina si un banco está vigente para el catálogo de
  mantenimiento;
- `BCO_FG_REAL` clasifica bancos reales y filtra ciertos escenarios de
  contrato/pago;
- no hay evidencia de que `BCO_FG_REAL` participe en la validación del
  `INSERT` o `UPDATE` de `CPCUENTA`;
- la futura API debe ofrecer bancos con estado activo y, para evitar bancos
  operativos del sistema, aplicar la clasificación real en la consulta del
  catálogo autorizada para clientes;
- el backend debe volver a validar el identificador activo; nunca confiar
  sólo en la lista enviada al navegador.

Los bancos activos clasificados como reales en la evidencia son: BANCOMER,
BANAMEX, BANAMEX TOYOTAFIN, HSBC, SANTANDER TC, SANTANDER SERFIN, SCOTIA
BANK, BANCO DEL BAJIO, BANCOMER DOMICILIACION, BANJERCITO, IXE BANCO,
INBURSA, INTERACCIONES, MIFEL, BANREGIO, INVEX, AFIRME, BANORTE, AMERICAN
EXPRESS, BANK OF AMERICA, BANK BOSTON, BANCO VE POR MÁS, BANAMEX
DOMICILIACION, BANCO J.P. MORGAN, BANCO AZTECA, BANCO FAMSA, BANCO MULTIVA,
NAFIN, INTERCAM BANCO, BANCOPPEL, CIBANCO, BANCOMEXT y SANTANDER
DOMICILIACION. Los identificadores y nombres completos no se incorporan a
DTOs de escritura: la API deberá obtenerlos de un catálogo Legacy vigente.

### País

La evidencia recibida identifica a México como país predeterminado:
`PAI_FL_CVE = 1`, código `MX` y `PAI_FG_REGDEFAULT = 1`. La consulta no
incluyó una columna de estatus para los países, por lo que no se puede
afirmar que el predeterminado equivalga por sí solo a “activo”. El backend
debe validar el país contra la fuente vigente confirmada; el repositorio no
hardcodeará `1` ni aceptará sin validación el valor inicial de la UI.

No se confirmó una relación funcional de `CPCUENTA` con `CPAIS_CUENTA`.
Mientras no exista esa evidencia, país no forma parte del DTO de cuentas ni
se usará para inventar una validación de cuenta.

### Medio de pago

`PCT_CL_MPAGO` es `int NULL`, no aparece en la UI ni en la firma del alta
Legacy revisada y las filas agregadas observadas tienen valor `NULL`. Los
catálogos 29 y 44 describen formas de pago de seguro y contrato, no un
catálogo inequívoco de medio de pago de `CPCUENTA`.

Por tanto, no se expondrá en la UI: las nuevas cuentas usarán `NULL`, y una
edición conservará el valor histórico existente. No se inventará catálogo ni
se sobrescribirá un valor histórico.

### Matriz definitiva para el contrato de aplicación

| Propiedad | Columna Legacy | Tipo/longitud | Regla de entrada y persistencia | Respuesta |
|---|---|---|---|---|
| `accountId` | `PCT_FL_CVE` | `int NOT NULL` | Sólo lo genera `CCATCONSEC` en alta; no se acepta del cliente | Sí |
| `bankId` | `BCO_FL_CVE` | `smallint NOT NULL` | Identificador de banco vigente; la aplicación valida estado y clasificación permitida | Sí |
| `branch` | `PCT_NO_SUCURSAL` | `smallint NOT NULL`; C# `short` | Obligatorio, numérico y dentro de `short`; no conserva ceros iniciales ni se relaciona con `CSUCURSAL` | Sí |
| `accountNumber` | `PCT_NO_CUENTA` | `varchar(20) NULL` | Obligatorio en alta; vacío/espacios inválido; nunca se devuelve completo | Sólo enmascarado |
| `clabe` | `PCT_NO_CLABE` | `varchar(20) NULL` | Obligatorio en alta; exactamente 18 dígitos; sin cálculo de dígito verificador por ahora | Sólo enmascarada |
| `currencyCode` | `PCT_CL_MONEDA` | `tinyint NOT NULL` | Catálogo 4 vigente: 1 PESOS, 2 DOLARES, 3 UDIS | Sí |
| `accountTypeCode` | `PCT_CL_TCUENTA` | `tinyint NOT NULL` | Catálogo 71 vigente: 1 CHEQUES, 2 TARJETA DE CREDITO, 3 TARJETA DE DEBITO | Sí |
| `paymentMethodCode` | `PCT_CL_MPAGO` | `int NULL` | Se conserva `NULL`; no existe catálogo confirmado | Sí como `null`, sin valor inventado |
| `status` | `PCT_FG_STATUS` | `tinyint NOT NULL` | Nuevas cuentas activas (`1`); `2` se trata como inactiva; no borrar físicamente | Sí |
| `modifiedAt` | `PCT_FE_ULTMOD` | `datetime NOT NULL` | Token de concurrencia; UPDATE condicionado por identificador y fecha | Sí |
| `personId` | `PNA_FL_PERSONA` | `int NOT NULL` | Se valida que la persona pertenezca al despliegue y esté autorizada | No se usa como filtro de empresa |

La regla de `branch` queda cerrada: el tipo físico `smallint` es
autoritativo, el modelo será `short`, el campo será obligatorio y no se
tratará como texto. Los ceros iniciales no forman parte del valor persistido.

### Duplicidad y estados

Queda aprobada la regla `409 account_duplicate`:

- en alta, cuenta y CLABE se comparan por separado contra filas activas de
  la misma persona;
- en edición sólo se validan los valores sensibles efectivamente
  reemplazados y se excluye el propio `PCT_FL_CVE`;
- edición de campos no sensibles no se bloquea por duplicados históricos;
- activación valida que el registro no genere un duplicado activo;
- no hay unicidad global ni corrección masiva de históricos;
- las detecciones se ejecutan dentro de la transacción y no registran los
  valores comparados.

Los conteos agregados de personas con duplicados activos (6,419 por cuenta
y 631 por CLABE) son evidencia de datos históricos y no se exponen en la
API.

### Integridad, transacciones y auditoría

La evidencia confirma que `CPCUENTA` no tiene FK físicas, no tiene triggers y
no tiene índices únicos adicionales a la PK clustered `PCT_FL_CVE`. La
relación con persona y la validez de banco, moneda, tipo y país deben
validarse en uCredit. La consistencia no puede delegarse a restricciones
físicas inexistentes.

Las operaciones futuras usarán una transacción que incluya validaciones,
consecutivo de alta, escritura, concurrencia y bitácora. Alta usará
actividad funcional `4`; modificación, activación y desactivación usarán
actividad funcional `5`. La referencia será técnica y no contendrá cuenta,
CLABE, sucursal ni payload. Los valores de control Legacy `23`, `24` y `25`
no se interpretan como `ATV_FL_CVE`.

`PCT_FE_ULTMOD` será el control de concurrencia: una actualización con fecha
esperada que afecte cero filas responde `409 account_modified`. Las
escrituras y la auditoría se revertirán juntas ante cualquier error.

### Plan definitivo listo para implementación

1. Validar la existencia del país en `CPAIS`; México (`1`) sólo será el
   valor inicial de UI y no la única opción.
2. Consultar bancos con `BCO_FG_STATUS = 1` y `BCO_FG_REAL = 1`,
   distinguiendo el catálogo de mantenimiento de los filtros de
   contrato/pago.
3. Mantener `PCT_CL_MPAGO = NULL` en altas y conservarlo en ediciones; no
   exponerlo mientras no exista un catálogo autoritativo.
4. Aplicar actividades funcionales 4/5; `23/24/25` permanecen como
   controles Legacy y no se interpretan como actividades.

El plan no tiene contradicciones físicas o funcionales pendientes: la
sucursal se modela como `short`, el país se valida por existencia en `CPAIS`
sin limitarlo a México, y el medio de pago no se expone.

No se implementó código productivo ni se ejecutaron SQL, POST, migraciones,
IdentityAdmin, commit o push como parte de esta revisión.
