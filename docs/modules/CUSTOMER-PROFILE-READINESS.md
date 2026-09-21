# Expediente de cliente y elegibilidad para captura de contratos

## Estado de esta etapa

La primera fase de lectura está implementada. La elegibilidad se calcula en
cada consulta a Legacy y no se persiste en Identity, `CPERSONA` ni otra tabla
auxiliar. No se agregan escrituras Legacy.

Un cliente sólo está listo cuando `CPERSONA.PNA_FG_STATUS = 1` y existen,
para el mismo `PNA_FL_PERSONA`, un domicilio con `DMO_FG_STATUS = 1`, un
teléfono con `TFN_FG_STATUS = 1` y una cuenta de `CPCUENTA` con
`PCT_FG_STATUS = 1`.

La consulta usa únicamente tres expresiones `EXISTS` para las colecciones; no selecciona ni carga
números de cuenta, CLABE, domicilios, teléfonos, correos u otra PII. Una fila
activa de `CPCUENTA` es suficiente conforme a la regla funcional aprobada: no
se exige `PCT_NO_CUENTA` ni `PCT_NO_CLABE`.

El contrato de aplicación es `CustomerProfileReadiness` con `PersonId`,
`HasGeneralData`, `HasAddress`, `HasPhone`, `HasAccount`, la propiedad
calculada `CanCreateContract` y `MissingRequirements`. La API expone sólo
los valores controlados `generalData`, `address`, `phone` y `account`.

Endpoint:

`GET /api/v1/customers/{personId}/readiness`

Requiere `customers.read`, sesión autenticada con tenant seleccionado y
coincidencia con el tenant del despliegue. La comprobación se realiza en
backend; no se usa `AllowedCompanyIds` para delimitar clientes. Persona
inexistente o inactiva, o sesión sin tenant seleccionado, produce `404`;
expediente completo o incompleto produce `200`.

La futura captura de contrato deberá volver a ejecutar esta regla en backend y
no confiar en una respuesta previa del navegador. El frontend sólo representa
el estado y mantiene deshabilitada la acción de captura.

La regla funcional aprobada es que un cliente sólo puede iniciar la captura de un contrato si tiene, como mínimo:

1. un domicilio válido y activo;
2. un teléfono válido y activo;
3. una cuenta válida y activa.

El backend será la autoridad. La interfaz sólo representará el resultado y mantendrá deshabilitada la acción cuando falte un requisito.

## Base actual confirmada

### Domicilio

La consulta existente de `LegacyCustomerReadRepository` carga el domicilio principal desde `dbo.CDOMICILIO` con:

```sql
DMO_FG_STATUS = 1 AND DMO_FG_REGDEFAULT = 1
```

Esto confirma la regla de domicilio activo y predeterminado para la ficha actual. Para readiness no debe exigirse que sea predeterminado: basta con que exista al menos un registro válido y activo. La futura consulta deberá usar `DMO_FG_STATUS = 1` y validar los campos mínimos confirmados del esquema, sin convertir la selección del principal en el requisito de elegibilidad.

El catálogo del tipo de domicilio se resuelve mediante `CPARAMETRO` con `PAR_FL_CVE = 7`. No se debe asumir que el tipo predeterminado sea necesario para cumplir este requisito.

### Teléfono

La consulta existente carga teléfonos de `dbo.CTELEFONO` con `TFN_FG_STATUS = 1`. La selección del principal se hace en el módulo con `TFN_FG_REGDEFAULT = 1` y el menor `TFN_FL_CVE` como desempate defensivo. El teléfono se relaciona funcionalmente sólo mediante `PNA_FL_PERSONA`; `DMO_FL_CVE` no forma parte del contrato, no se lee y no se usa para determinar la elegibilidad. La evidencia efectiva de `pr_t` confirma que esa columna es `NOT NULL`; las ediciones la conservan y las altas de teléfonos permanecen bloqueadas hasta confirmar su valor técnico requerido.

Para readiness, el requisito es la existencia de al menos un teléfono activo válido; no es necesario que exista un teléfono predeterminado. Los campos de contacto no deben aparecer en logs de diagnóstico.

### Cuenta

La tabla principal confirmada es `dbo.CPCUENTA`. La evidencia recibida muestra una relación directa por `PNA_FL_PERSONA`:

| Columna | Tipo SQL | Nullable | Función confirmada |
|---|---|---:|---|
| `PCT_FL_CVE` | `int` | No | Identificador de cuenta; PK física y consecutivo registrado en `CCATCONSEC` |
| `BCO_FL_CVE` | `smallint` | No | Clave de banco |
| `PCT_NO_SUCURSAL` | `smallint` | No | Sucursal |
| `PCT_NO_CUENTA` | `varchar(20)` | Sí | Número de cuenta; no debe aparecer en respuestas ni logs |
| `PCT_NO_CLABE` | `varchar(20)` | Sí | CLABE; no debe aparecer en respuestas ni logs |
| `PCT_CL_MONEDA` | `tinyint` | No | Clave de moneda |
| `PCT_CL_TCUENTA` | `tinyint` | No | Tipo de cuenta |
| `PCT_FG_STATUS` | `tinyint` | No | Estado de la cuenta |
| `PCT_FE_ULTMOD` | `datetime` | No | Fecha de última modificación |
| `USR_CL_CVE` | `varchar(8)` | Sí | Usuario Legacy |
| `PNA_FL_PERSONA` | `int` | No | Relación con `CPERSONA.PNA_FL_PERSONA` |
| `PCT_DS_INST_DEPOSITO` | `varchar(255)` | Sí | Institución de depósito |
| `PCT_CL_MPAGO` | `int` | Sí | Medio de pago |

La PK física queda confirmada por el índice clustered único `PK__CPCUENTA__318258D2` sobre `PCT_FL_CVE`. La evidencia no marca esa columna como identity. `CCATCONSEC` confirma el mecanismo de consecutivo:

| `EMP_FL_CVE` | `CCS_DS_NOMTABLA` | `CCT_NO_CONSECUTIVO` | `CCT_DS_CAMPO` | `ID_FL_CVE` |
|---:|---|---:|---|---|
| `0` | `CPCUENTA` | `497241` al momento de la evidencia | `PCT_FL_CVE` | `PCT_FL_CVE` |

La evidencia de columnas no incluye `is_identity`, por lo que el identificador no debe tratarse como identity. Tampoco se recibió una columna de cuenta predeterminada en `CPCUENTA`.

`PCT_FG_STATUS = 1` queda confirmado como estado activo y `PCT_FG_STATUS = 2` como el otro estado observado. Esto proviene de la evidencia funcional entregada, no de una inferencia de frecuencia. No se deben usar números de cuenta ni CLABE para inferir la validez.

La relación lógica con la persona queda confirmada por `CPCUENTA.PNA_FL_PERSONA` (`int NOT NULL`) y `CPERSONA.PNA_FL_PERSONA` (`int NOT NULL`), además del índice no único `PERSONA` sobre `CPCUENTA.PNA_FL_PERSONA`. La evidencia de llaves foráneas recibida no incluye `CPCUENTA`, por lo que no se confirma una FK física; la relación física pendiente debe verificarse con `sys.foreign_key_columns`.

`CBANCO` queda confirmado como maestro de bancos por su estructura y por la coincidencia `BCO_FL_CVE`, pero la FK o regla de consumo desde `CPCUENTA` no está confirmada. `CBCO_CTAS` y `CPAIS_CUENTA` quedan como tablas relacionadas candidatas; la evidencia no demuestra que sean FK o catálogos consumidos por `CPCUENTA`. `PCT_CL_MONEDA`, `PCT_CL_TCUENTA` y `PCT_CL_MPAGO` no tienen catálogo/descripción funcional confirmados.

La evidencia muestra `PCT_CL_MONEDA = 1` y tipos de cuenta `1`, `2` y `3`, pero esos valores no se convierten en catálogos ni reglas únicamente por distribución. `PCT_CL_MPAGO` aparece nullable y sin valores no nulos en la distribución recibida; no es requisito para readiness mientras no exista una regla funcional que lo exija.

### Campos obligatorios de la fila de cuenta

Por nulabilidad, los campos obligatorios de `CPCUENTA` son: `PCT_FL_CVE`, `BCO_FL_CVE`, `PCT_NO_SUCURSAL`, `PCT_CL_MONEDA`, `PCT_CL_TCUENTA`, `PCT_FG_STATUS`, `PCT_FE_ULTMOD` y `PNA_FL_PERSONA`. `PCT_NO_CUENTA`, `PCT_NO_CLABE`, `USR_CL_CVE`, `PCT_DS_INST_DEPOSITO` y `PCT_CL_MPAGO` son nullable.

La nulabilidad no define por sí sola la validez funcional. Sigue pendiente decidir, con evidencia de código Legacy o una regla aprobada, si una cuenta activa debe además:

- referenciar un banco existente y activo mediante `BCO_FL_CVE`;
- tener `PCT_NO_CUENTA`, `PCT_NO_CLABE` o ambos;
- validar formato o longitud más allá del esquema;
- validar moneda y tipo de cuenta contra catálogos específicos.

La distribución entregada indica que las filas activas tienen número de cuenta y al menos un identificador de cuenta, pero no se usa como sustituto de la regla funcional. Por tanto, todavía no se puede afirmar que baste una fila con `PCT_FG_STATUS = 1`, ni que deba exigirse una combinación concreta de banco y cuenta/CLABE.

No existe regla de cuenta predeterminada confirmada: `CPCUENTA` no tiene una columna `DEFAULT`/`REGDEFAULT` en la evidencia, y el índice `PERSONA` no expresa tal semántica. Readiness sólo requiere una cuenta válida y activa; no requiere una cuenta principal.

## Contrato de aplicación propuesto

El módulo `Customers` deberá exponer un modelo neutral, sin tipos SQL ni DTOs HTTP:

```csharp
public sealed record CustomerProfileReadiness(
    int PersonId,
    bool HasGeneralData,
    bool HasAddress,
    bool HasPhone,
    bool HasAccount,
    bool CanCreateContract,
    IReadOnlyList<string> MissingRequirements);
```

Semántica propuesta:

| Propiedad | Regla propuesta | Estado |
|---|---|---|
| `PersonId` | `CPERSONA.PNA_FL_PERSONA` | Confirmado |
| `HasGeneralData` | Existe una persona activa y sus campos generales requeridos pasan la validación de lectura | Pendiente de fijar campos mínimos |
| `HasAddress` | Existe al menos un domicilio válido con `DMO_FG_STATUS = 1` | Regla de estatus confirmada; definición de “válido” pendiente de matriz completa |
| `HasPhone` | Existe al menos un teléfono válido con `TFN_FG_STATUS = 1` | Regla de estatus confirmada; definición de “válido” pendiente de esquema completo |
| `HasAccount` | Existe al menos una fila de `CPCUENTA` vinculada por `PNA_FL_PERSONA`, con estado activo confirmado y campos mínimos válidos | Tabla y relación confirmadas; valor activo y campos mínimos pendientes |
| `CanCreateContract` | `HasGeneralData && HasAddress && HasPhone && HasAccount` | Derivada en backend, no persistida |
| `MissingRequirements` | Lista técnica estable, sin PII: `general_data`, `address`, `phone`, `account` | Propuesta |

La lista debe tener orden estable: `general_data`, `address`, `phone`, `account`. Cuando todos los requisitos se cumplen debe ser una lista vacía.

## Endpoint propuesto

```http
GET /api/v1/customers/{personId}/readiness
```

Características:

- autorización `customers.read`;
- sesión autenticada y tenant seleccionado mediante la cookie firmada;
- resolución del tenant exclusivamente con `IExecutionTenantContext`;
- validación del alcance autorizado antes de devolver el expediente;
- `personId` entero positivo y parametrizado;
- `404 Not Found` si la persona no existe o está fuera del alcance;
- `200 OK` con el contrato `CustomerProfileReadiness` si existe y es visible;
- `401` para sesión anónima y `403` cuando falte `customers.read`;
- `ProblemDetails` para errores de validación o infraestructura;
- sin RFC, teléfonos, correos, domicilios, cuentas ni datos de conexión en errores o logs.

Ejemplo controlado de respuesta incompleta:

```json
{
  "type": "https://ucredit.invalid/problems/customer-profile-incomplete",
  "title": "Customer profile is incomplete",
  "status": 422,
  "code": "customer_profile_incomplete",
  "missingRequirements": ["address", "account"]
}
```

El `422` se reservará para la acción posterior de captura de contrato cuando el expediente no sea elegible. La consulta de readiness puede devolver `200` con `canCreateContract: false`; no debe convertir la consulta en una mutación ni en una autorización del navegador.

## SQL de sólo lectura pendiente para cuentas

Estas consultas son plantillas de investigación. No se ejecutaron y no deben convertirse en SQL de producción sin confirmar los resultados y la regla funcional.

### 1. Tablas y columnas candidatas

```sql
SELECT
    s.name AS SchemaName,
    t.name AS TableName,
    c.column_id,
    c.name AS ColumnName,
    ty.name AS SqlType,
    c.max_length,
    c.precision,
    c.scale,
    c.is_nullable,
    c.is_identity,
    dc.definition AS DefaultDefinition
FROM sys.tables AS t
JOIN sys.schemas AS s ON s.schema_id = t.schema_id
JOIN sys.columns AS c ON c.object_id = t.object_id
JOIN sys.types AS ty ON ty.user_type_id = c.user_type_id
LEFT JOIN sys.default_constraints AS dc ON dc.parent_object_id = c.object_id
    AND dc.parent_column_id = c.column_id
WHERE s.name = 'dbo'
  AND (
      c.name LIKE '%CUENTA%'
      OR c.name LIKE '%ACCOUNT%'
      OR c.name LIKE '%CLABE%'
      OR c.name LIKE '%BANCO%'
      OR c.name LIKE '%STATUS%'
      OR c.name LIKE '%ACTIV%'
      OR c.name LIKE '%DEFAULT%'
  )
ORDER BY t.name, c.column_id;
```

La salida sólo contiene metadatos; no debe agregarse una tabla al código por aparecer en esta búsqueda.

### 2. Relaciones con `PNA_FL_PERSONA`

```sql
SELECT
    sch_child.name AS ChildSchema,
    child_tab.name AS ChildTable,
    child_col.name AS ChildColumn,
    fk.name AS ForeignKeyName,
    sch_parent.name AS ParentSchema,
    parent_tab.name AS ParentTable,
    parent_col.name AS ParentColumn,
    fk.delete_referential_action_desc AS DeleteAction,
    fk.update_referential_action_desc AS UpdateAction,
    fk.is_disabled
FROM sys.foreign_keys AS fk
JOIN sys.foreign_key_columns AS fkc ON fkc.constraint_object_id = fk.object_id
JOIN sys.tables AS child_tab ON child_tab.object_id = fkc.parent_object_id
JOIN sys.schemas AS sch_child ON sch_child.schema_id = child_tab.schema_id
JOIN sys.columns AS child_col ON child_col.object_id = fkc.parent_object_id
    AND child_col.column_id = fkc.parent_column_id
JOIN sys.tables AS parent_tab ON parent_tab.object_id = fkc.referenced_object_id
JOIN sys.schemas AS sch_parent ON sch_parent.schema_id = parent_tab.schema_id
JOIN sys.columns AS parent_col ON parent_col.object_id = fkc.referenced_object_id
    AND parent_col.column_id = fkc.referenced_column_id
WHERE sch_child.name = 'dbo'
  AND (child_col.name = 'PNA_FL_PERSONA' OR parent_col.name = 'PNA_FL_PERSONA')
ORDER BY child_tab.name, fk.name;
```

Si no aparece una FK, la relación sólo podrá aceptarse con evidencia de código Legacy y pruebas de integridad; no se inferirá por nombre.

### 3. PK, índices y columnas de estado

```sql
SELECT
    s.name AS SchemaName,
    t.name AS TableName,
    i.name AS IndexName,
    i.type_desc,
    i.is_unique,
    i.is_primary_key,
    i.is_unique_constraint,
    ic.key_ordinal,
    c.name AS ColumnName,
    ic.is_included_column
FROM sys.tables AS t
JOIN sys.schemas AS s ON s.schema_id = t.schema_id
JOIN sys.indexes AS i ON i.object_id = t.object_id
JOIN sys.index_columns AS ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
JOIN sys.columns AS c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
WHERE s.name = 'dbo'
  AND (c.name = 'PNA_FL_PERSONA' OR c.name LIKE '%CUENTA%' OR c.name LIKE '%CLABE%'
       OR c.name LIKE '%STATUS%' OR c.name LIKE '%ACTIV%' OR c.name LIKE '%DEFAULT%')
ORDER BY t.name, i.name, ic.key_ordinal, ic.index_column_id;
```

### 4. Consecutivos y catálogos relacionados

```sql
SELECT EMP_FL_CVE, CCS_DS_NOMTABLA, CCT_NO_CONSECUTIVO, USR_CL_CVE, CCT_DS_CAMPO, ID_FL_CVE
FROM dbo.CCATCONSEC
WHERE CCS_DS_NOMTABLA LIKE '%CUENTA%'
   OR CCS_DS_NOMTABLA LIKE '%ACCOUNT%'
ORDER BY CCS_DS_NOMTABLA, EMP_FL_CVE;
```

```sql
SELECT PAR_FL_CVE, PAR_CL_VALOR, PAR_DS_DESCRIPCION, PAR_FG_STATUS, PAR_FG_REGDEFAULT
FROM dbo.CPARAMETRO
WHERE PAR_DS_DESCRIPCION LIKE '%CUENTA%'
   OR PAR_DS_DESCRIPCION LIKE '%BANCO%'
   OR PAR_DS_DESCRIPCION LIKE '%CLABE%'
   OR PAR_DS_DESCRIPCION LIKE '%ACTIV%'
ORDER BY PAR_FL_CVE, PAR_CL_VALOR;
```

### 5. Distribución segura, después de identificar una tabla

Una vez aprobada una tabla y sus columnas, la distribución debe devolver sólo conteos agrupados por códigos de estado/default. No se deben seleccionar números de cuenta, CLABE, nombres, RFC, correos o domicilios.

```sql
-- Sustituir los identificadores únicamente por nombres confirmados y revisados.
SELECT StatusCode, DefaultCode, COUNT_BIG(*) AS Total
FROM dbo.<ConfirmedAccountTable>
WHERE <ConfirmedPersonColumn> = @PersonId
GROUP BY StatusCode, DefaultCode;
```

El parámetro `@PersonId` debe ser `int`. No se permite concatenar el valor recibido ni ejecutar esta plantilla con un identificador de tabla no validado.

### 6. Consultas adicionales específicas de `CPCUENTA`

La siguiente consulta confirma nulabilidad, identidad y defaults sin leer valores de cuenta:

```sql
SELECT
    c.column_id,
    c.name AS ColumnName,
    ty.name AS SqlType,
    c.max_length,
    c.precision,
    c.scale,
    c.is_nullable,
    c.is_identity,
    dc.definition AS DefaultDefinition
FROM sys.tables AS t
JOIN sys.schemas AS s ON s.schema_id = t.schema_id
JOIN sys.columns AS c ON c.object_id = t.object_id
JOIN sys.types AS ty ON ty.user_type_id = c.user_type_id
LEFT JOIN sys.default_constraints AS dc ON dc.parent_object_id = c.object_id
    AND dc.parent_column_id = c.column_id
WHERE s.name = 'dbo' AND t.name = 'CPCUENTA'
ORDER BY c.column_id;
```

La siguiente consulta confirma PK, unique constraints e índices que puedan afectar la selección:

```sql
SELECT
    i.name AS IndexName,
    i.type_desc,
    i.is_unique,
    i.is_primary_key,
    i.is_unique_constraint,
    ic.key_ordinal,
    c.name AS ColumnName,
    ic.is_included_column
FROM sys.tables AS t
JOIN sys.schemas AS s ON s.schema_id = t.schema_id
JOIN sys.indexes AS i ON i.object_id = t.object_id
JOIN sys.index_columns AS ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
JOIN sys.columns AS c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
WHERE s.name = 'dbo' AND t.name = 'CPCUENTA'
ORDER BY i.name, ic.key_ordinal, ic.index_column_id;
```

La relación física con la persona y posibles relaciones con banco/catálogos se verifica sin consultar datos:

```sql
SELECT
    fk.name AS ForeignKeyName,
    OBJECT_SCHEMA_NAME(fkc.parent_object_id) AS ChildSchema,
    OBJECT_NAME(fkc.parent_object_id) AS ChildTable,
    COL_NAME(fkc.parent_object_id, fkc.parent_column_id) AS ChildColumn,
    OBJECT_SCHEMA_NAME(fkc.referenced_object_id) AS ParentSchema,
    OBJECT_NAME(fkc.referenced_object_id) AS ParentTable,
    COL_NAME(fkc.referenced_object_id, fkc.referenced_column_id) AS ParentColumn,
    fk.is_disabled
FROM sys.foreign_keys AS fk
JOIN sys.foreign_key_columns AS fkc ON fkc.constraint_object_id = fk.object_id
WHERE OBJECT_SCHEMA_NAME(fkc.parent_object_id) = 'dbo'
  AND OBJECT_NAME(fkc.parent_object_id) = 'CPCUENTA';
```

Después de confirmar los códigos de estado y catálogos, la distribución segura debe agrupar únicamente por códigos. No se deben seleccionar `PCT_NO_CUENTA`, `PCT_NO_CLABE` ni `PCT_DS_INST_DEPOSITO`:

```sql
SELECT PCT_FG_STATUS, PCT_CL_MONEDA, PCT_CL_TCUENTA, PCT_CL_MPAGO, COUNT_BIG(*) AS Total
FROM dbo.CPCUENTA
WHERE PNA_FL_PERSONA = @PersonId
GROUP BY PCT_FG_STATUS, PCT_CL_MONEDA, PCT_CL_TCUENTA, PCT_CL_MPAGO;
```

La consulta anterior es sólo de lectura y sólo será útil después de confirmar qué códigos representan estado activo y qué combinaciones son válidas.

## Alcance tenant

Cada despliegue tiene un sitio y una base Legacy independientes. El tenant seleccionado debe coincidir con `Deployment__TenantCode`, y la conexión Legacy está configurada por despliegue. `EMP_FL_CVE` controla las carteras dentro de esa instalación.

`IExecutionTenantContext` valida la membresía activa seleccionada y el código de tenant permitido. Mientras no exista una relación confirmada entre `CPERSONA` y empresa, Customers se delimita por tenant/despliegue y no por `AllowedCompanyIds`. No se debe inventar un filtro de empresa para clientes.

El endpoint no aceptará tenant por navegador ni por header. La consulta usará la conexión del despliegue y el contexto de sesión ya validado. `AllowedCompanyIds` seguirá siendo obligatorio para contratos, pero no se aplicará a Customers sin evidencia de una relación empresa-persona.

## UX propuesta

La ficha del cliente tendrá cuatro tarjetas de estado:

| Tarjeta | Estado backend | Representación |
|---|---|---|
| Domicilios | `HasAddress` | “Completo” o “Falta domicilio activo” |
| Teléfonos | `HasPhone` | “Completo” o “Falta teléfono activo” |
| Correos | Información complementaria, no requisito aprobado | Se muestra sin alterar elegibilidad |
| Cuentas | `HasAccount` | “Completo” o “Falta cuenta activa” |

La acción “Capturar contrato” permanecerá deshabilitada si `CanCreateContract` es falso, con `disabled` y `aria-disabled="true"`. La interfaz no calculará ni enviará una autorización; sólo consumirá el resultado del endpoint. Un `401` regresará al login, un `403` mostrará acceso no autorizado y un `404` indicará que el cliente no existe o no pertenece al alcance visible.

## Plan por fases

1. **Descubrimiento:** ejecutar las consultas de metadatos sólo con autorización y confirmar tabla, columnas, FK, índices, estados, default, catálogo y consecutivo de cuentas.
2. **Trazabilidad Legacy:** localizar la pantalla/evento o servicio que define cuenta válida/activa y registrar la regla sin copiar código propietario.
3. **Contrato de aplicación:** agregar `CustomerProfileReadiness` e interfaz de consulta sin dependencia de `LegacySql`.
4. **Adaptador de lectura:** cargar persona, domicilios, teléfonos y cuenta con consultas separadas, parametrizadas y sin multiplicar clientes.
5. **API:** proyectar a DTO HTTP explícito y aplicar `customers.read`, tenant y alcance; devolver `404` fuera de alcance.
6. **Pruebas:** autorización, tenant, inexistencia, alcance, combinaciones de requisitos, nulabilidad inesperada y ausencia de PII.
7. **Frontend:** tarjetas y acción deshabilitada; ningún estado local será autoridad.

No se habilitarán escrituras Legacy, migraciones, IdentityAdmin ni cambios de esquema en estas fases.

## Pruebas propuestas

- `401` sin autenticación y `403` sin `customers.read`.
- Tenant seleccionado A no puede consultar personas del tenant B.
- Membresía activa y `AllowedCompanyIds` se toman de `IExecutionTenantContext`.
- `404` para persona inexistente y persona fuera del alcance.
- combinaciones de los tres requisitos: todos presentes, falta uno, faltan dos y faltan los tres.
- domicilio activo/inactivo y múltiples domicilios.
- teléfono activo/inactivo, con y sin predeterminado; la elegibilidad no depende del predeterminado.
- cuenta activa/inactiva y estados desconocidos, una vez confirmado el esquema.
- nulabilidad inesperada en columnas requeridas produce fallo cerrado y no una elegibilidad falsa.
- SQL parametrizado, sin `SELECT *`, sin `NOLOCK`, sin concatenación de entrada y sin joins multiplicadores.
- frontend representa `MissingRequirements`, conserva branding/sesión y no habilita la captura por decisión propia.

## Riesgos y decisiones pendientes

1. **Banco y catálogos:** la regla de readiness no requiere validar banco, moneda, tipo de cuenta ni medio de pago; esos catálogos quedan pendientes para una futura administración de cuentas.
2. **Cuenta predeterminada:** `CPCUENTA` no muestra un indicador predeterminado; la regla aprobada sólo exige una cuenta activa.
3. **Alcance tenant:** queda resuelto por despliegue/tenant, sin filtro `AllowedCompanyIds` para Customers mientras no exista relación empresa-persona confirmada.
4. **Correos:** se muestran como información del expediente, pero no forman parte de la regla funcional aprobada.

## Decisiones aprobadas para administración de domicilios

La evidencia agregada de `CDOMICILIO` y la revisión de `RevisarCboTD` en
`su_MtoDireccion.aspx.vb`, junto con `ActualizaDomicilio` en
`sd_clsPersona.vb`, confirman las siguientes reglas:

- toda persona que tenga domicilios debe conservar exactamente un domicilio
  activo con `DMO_FG_REGDEFAULT = 1`;
- el primer domicilio se crea activo y predeterminado;
- al seleccionar un nuevo predeterminado se desmarcan los anteriores dentro
  de la misma transacción;
- no se permite desactivar el predeterminado sin proporcionar un domicilio
  activo de reemplazo; la respuesta será `409` con código
  `address_default_required`;
- `DMO_FE_ULTMOD` será el control de concurrencia optimista;
- PUT, activación y desactivación recibirán `expectedModifiedAt`;
- el `UPDATE` incluirá la fecha esperada y una actualización de cero filas
  responderá `409` con código `address_modified`;
- el alta usará actividad `4`; modificación, activación, desactivación y
  cambio de predeterminado usarán actividad `5`;
- la bitácora reutilizará el mecanismo existente y nunca incluirá domicilio,
  código postal, referencias ni payload.

### Banderas de uso del domicilio

La pantalla Legacy no trata las tres banderas como un catálogo simple cuyo
valor pueda obtenerse solamente de `DMO_FG_TDIRECCION`. `RevisarCboTD` fija
algunas opciones y deja otras habilitadas; además, al editar un registro
recupera usos existentes antes de aplicar las restricciones del tipo. La
distribución agregada confirma que existen múltiples combinaciones dentro de
un mismo tipo, por lo que la frecuencia no se usa como regla.

| Tipo | `DMO_FG_FACTURA` | `DMO_FG_EDOCTA` | `DMO_FG_OTROS` | Contrato funcional | Estado |
|---:|---:|---:|---:|---|---|
| 1, Dirección única | `1` | `1` | `1` | `billing`, `statements` y `other`, seleccionados y bloqueados | Aprobado |
| 2, Dirección fiscal | `1` | Según `statements` | Según `other` | `billing` obligatorio y bloqueado; los otros dos son opcionales | Aprobado |
| 3, Dirección administrativa | `0` | Según `statements` | Según `other` | `billing` no disponible; los otros dos son opcionales | Aprobado |
| 4, Dirección social | `0` | Según `statements` | Según `other` | `billing` no disponible; los otros dos son opcionales | Aprobado |

La distribución recibida respalda esta lectura: tipo 1 concentra la
combinación `1/1/1`; tipo 2 contiene combinaciones distintas de Estado de
Cuenta y Otros; y tipos 3 y 4 contienen combinaciones distintas de esas dos
banderas. Esto demuestra que no es correcto implementar una función que
derive las tres banderas exclusivamente desde el tipo.

El contrato moderno representa usos funcionales y no columnas Legacy. El
request llevará únicamente:

```json
{ "uses": ["billing", "statements", "other"] }
```

El backend aceptará solamente esos tres valores, normalizará espacios y
duplicados, rechazará valores desconocidos y derivará internamente las
columnas Legacy. Nunca aceptará `DMO_FG_FACTURA`, `DMO_FG_EDOCTA` ni
`DMO_FG_OTROS` desde el request.

Las reglas de derivación son:

- tipo 1 siempre produce `1/1/1` y exige los tres usos;
- tipo 2 siempre produce Factura `1`, exige `billing` y permite seleccionar
  `statements` y `other`;
- tipos 3 y 4 siempre producen Factura `0`, rechazan `billing` y permiten
  guardar sin usos adicionales; `statements` y `other` son opcionales.

La respuesta tampoco expondrá nombres de columnas Legacy; cuando sea
necesario representar usos, utilizará los valores funcionales controlados.

### Contrato de concurrencia

Las respuestas de lectura expondrán `modifiedAt` únicamente como token
técnico para una mutación autorizada. Las mutaciones deberán comparar
`expectedModifiedAt` con `DMO_FE_ULTMOD` dentro de la misma transacción y
revisar el número de filas afectadas. La conversión deberá respetar la
precisión real de `datetime` y no asumir UTC mientras no se confirme que el
servidor Legacy almacena esa columna en UTC.

Al cambiar el predeterminado, la desactivación del anterior y la activación
del nuevo formarán una sola unidad transaccional. Si el domicilio esperado ya
fue modificado, se hará rollback y se devolverá `address_modified`. Si se
intenta dejar sin predeterminado activo a la persona, se hará rollback y se
devolverá `address_default_required`.

### Plan final condicionado

Con la matriz aprobada, la implementación seguirá estas fases:

1. agregar el contrato de aplicación y DTOs explícitos para la colección de
   domicilios;
2. implementar la consulta parametrizada sin joins multiplicadores;
3. implementar alta, edición y acciones de estado con `CCATCONSEC` sólo en
   altas;
4. aplicar las reglas de predeterminado y `expectedModifiedAt` dentro de la
   transacción;
5. escribir la bitácora con actividad 4 o 5 sin PII;
6. recalcular readiness después de cada mutación;
7. integrar la tarjeta y panel accesible en el expediente;
8. cubrir autorización, antiforgery, tenant, rollback, concurrencia y doble
   envío.

### Contrato de endpoints

```text
GET  /api/v1/customers/{personId}/addresses
POST /api/v1/customers/{personId}/addresses
PUT  /api/v1/customers/{personId}/addresses/{addressId}
POST /api/v1/customers/{personId}/addresses/{addressId}/activate
POST /api/v1/customers/{personId}/addresses/{addressId}/deactivate
```

GET requiere `customers.read`. Las mutaciones requieren `customers.write`,
antiforgery, tenant seleccionado, tenant coincidente con el despliegue,
`LegacyUserCode` de la membresía activa, entorno Development, conexión de
escritura y guardas explícitas de `pr_t`.

Los payloads de alta y edición incluirán `uses`, no las tres banderas. El
servidor validará el tipo, resolverá los usos y ejecutará la derivación antes
de abrir la operación Legacy.

### Transacciones

Cada mutación se ejecutará en una única transacción Legacy. El alta reservará
el consecutivo de `CDOMICILIO` mediante `CCATCONSEC`; PUT, activación,
desactivación y cambio de predeterminado no reservarán uno nuevo. El cambio
de predeterminado desmarcará el anterior y marcará el nuevo dentro de la
misma transacción.

Todas las actualizaciones incluirán `PNA_FL_PERSONA`, `DMO_FL_CVE` y
`expectedModifiedAt` en la condición. Cero filas afectadas producirá
`409 address_modified`. Desactivar el único predeterminado sin reemplazo
producirá `409 address_default_required`. La bitácora utilizará actividad 4
en altas y actividad 5 en las demás mutaciones, sin domicilio ni payload.

### UI y pruebas

El panel accesible mostrará “Facturación”, “Estado de cuenta” y “Otros”.
Cambiar el tipo recalculará la disponibilidad y selección: tipo 1 bloqueará
los tres; tipo 2 bloqueará Facturación; tipos 3 y 4 ocultarán o deshabilitarán
Facturación y permitirán guardar sin usos adicionales. El frontend impedirá
doble envío y refrescará domicilios y readiness después de guardar.

Las pruebas cubrirán derivación exacta por tipo, usos duplicados o
desconocidos, `billing` inválido para tipos 3 y 4, alta sin usos para tipos 3
y 4, antiforgery, permisos, tenant, consecutivo sólo en alta, bitácora,
rollback, concurrencia, reglas de predeterminado, códigos 400/401/403/404/
409/503, ausencia de PII en logs y prevención de doble envío.

La matriz funcional queda aprobada, pero el código productivo permanece sin
cambios en esta etapa, conforme a la instrucción de revisión previa.

La administración de cuentas recalcula `hasAccount` desde
`CPCUENTA.PCT_FG_STATUS = 1` después de cada mutación; no persiste una
bandera de elegibilidad en Identity ni en Legacy.
# Administración de teléfonos

La administración de teléfonos mantiene el requisito `phone` calculado en
tiempo de consulta. Después de cada alta, edición, activación o
desactivación, el expediente debe refrescar readiness; no se persiste una
bandera duplicada en Identity ni en Legacy.

La administración de teléfonos está desacoplada de domicilios. `HasPhone`
depende exclusivamente de `CTELEFONO.TFN_FG_STATUS = 1`, mientras que
`HasAddress` depende exclusivamente de `CDOMICILIO.DMO_FG_STATUS = 1`.
Ningún teléfono satisface el requisito de domicilio y ningún domicilio
satisface el requisito de teléfono; ambas expresiones `EXISTS` se calculan
por separado.

Las nuevas operaciones usan estado activo `1`, inactivo `2` e histórico `0`
(`Inactivo heredado`). El estado `0` sólo puede activarse. La operación de
desactivación conserva el requisito de un predeterminado activo y devuelve
`409 phone_default_required` si no se proporciona un reemplazo. La
concurrencia usa `TFN_FE_ULTMOD` y devuelve `409 phone_modified`.
