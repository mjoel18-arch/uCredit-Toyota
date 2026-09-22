# Administración de datos generales y roles de Customer

## Alcance de esta revisión

Este documento prepara la siguiente etapa de edición de una persona/cliente y
de administración de sus roles. No implementa endpoints, SQL ni escrituras.
La entidad funcional continúa siendo `Customer`, identificada por
`CPERSONA.PNA_FL_PERSONA`; “Prospecto” no es una entidad nueva.

La rama revisada es `feature/customer-general-management`. El código moderno
actual dispone de consultas de Customer y del alta transaccional, pero no de
un caso de uso de edición. `Customer` expone un resumen de persona, roles,
domicilio, teléfono y correos; no expone todavía un modelo editable de datos
generales ni un token de concurrencia para persona/subtipo.

## Evidencia revisada

### Implementación moderna

- `Customer` contiene `PersonId`, RFC, nombre, personalidad, estatus, roles y
  colecciones del expediente.
- `LegacyCustomerReadRepository` consulta `CPERSONA`, `CPFISICA`, `CPMORAL`,
  `CPTIPO` y catálogos para lectura; los modelos SQL se materializan en filas
  de infraestructura antes de mapearse al módulo.
- `CustomerCreateCommand` contiene los datos de alta de persona, personalidad,
  RFC, grupo, riesgo, país, forma de contacto, régimen fiscal y roles.
- `LegacyCustomerWriteRepository` sólo expone actualmente `CreateAsync` y
  conserva la transacción de `CPERSONA`, subtipo, `CPTIPO` y bitácora.
- El frontend muestra el detalle y los módulos independientes de domicilio,
  teléfono, cuenta y correo; no debe mezclar sus mutaciones con datos
  generales ni roles.

### Código Legacy consultado

La trazabilidad de modificación se encontró en:

- `Sitio Web/Migrado/su_MtoPersona.aspx.vb`, evento `cmdGuardar_Click`.
- `snLsenet/sn_clsPersona.vb`, método `ActualizaPersona`.
- `sdLsenet/sd_clsPersona.vb`, método `ActualizaPersona`.
- `sdLsenet/sd_clsPersona.vb`, consultas de personas y roles.

La pantalla asigna `CommandArgumentControl = 4` en alta y `5` en edición.
Esto es autorización de la operación Legacy; no se interpreta como un código
de actividad de bitácora diferente del que finalmente registra el mecanismo
de seguridad.

La implementación Legacy observada actualiza dentro de una transacción:

1. `CPERSONA`.
2. `CPMORAL` o `CPFISICA`.
3. elimina las filas de `CPTIPO` de la persona.
4. inserta nuevamente las filas seleccionadas de `CPTIPO`.
5. ejecuta lógica adicional para el rol aseguradora cuando aplica.
6. confirma o revierte la transacción.

La fuente usa SQL concatenado; uCredit no debe copiar ese mecanismo. La
equivalencia funcional debe implementarse con parámetros tipados y validación
previa.

## Matriz de CPERSONA

La siguiente clasificación separa lo observado en el flujo Legacy de lo que
requiere aprobación para el contrato moderno. Los tipos y nulabilidad
definitivos deben confirmarse con `sys.columns` de la base de despliegue antes
de implementar.

| Campo Legacy | Evidencia de uso | Propuesta inicial | Estado |
|---|---|---|---|
| `PNA_FL_PERSONA` | Identificador usado por consultas, subtipo y roles | Inmutable; sólo ruta y respuesta | Confirmado funcionalmente |
| `PNA_CL_PJURIDICA` | La pantalla selecciona personalidad y Legacy compara la anterior | Inmutable después del alta en la primera versión | Cambio bloqueado hasta confirmar conversión segura |
| `PNA_CL_RFC` | Se valida y actualiza en `ActualizaPersona`; el alta usa bloqueo de duplicado | No editable inicialmente; una futura edición requiere duplicado, impacto y auditoría | Decisión funcional pendiente |
| `PNA_DS_NOMBRE` | Nombre resumen se recompone desde datos físicos o razón social | Derivado; no aceptar edición independiente | Confirmado como representación |
| `PNA_FG_FCONTACTO` | La pantalla selecciona forma de contacto | Editable mediante catálogo activo confirmado | Falta confirmar catálogo y valores |
| `GPR_FL_CVE` | La pantalla exige grupo y Legacy lo actualiza | Editable mediante catálogo activo confirmado | Falta confirmar esquema/catálogo |
| `GRI_FL_CVE` | La pantalla exige riesgo y Legacy lo actualiza | Editable mediante catálogo activo confirmado | Falta confirmar esquema/catálogo y efectos |
| `PAI_FL_CVE` | Se usa para reglas de RFC y se actualiza | Editable sólo con catálogo y efectos de RFC confirmados | Falta confirmar política |
| `PNA_FG_STATUS` | La pantalla valida estatus; el catálogo 1 documenta activo/inactivo | Acción controlada de activar/desactivar, no edición libre | Efectos sobre contratos pendientes |
| `PNA_FE_ALTA` | Se escribe en alta | Inmutable | Confirmado funcionalmente |
| `PNA_FE_ULTMOD` | Legacy la actualiza al modificar persona | Token de concurrencia de `CPERSONA` | Precisión/tipo físico pendientes |
| `USR_CL_CVE` | Legacy lo escribe desde usuario | Lo asigna el servidor desde `LegacyUserCode` | Nunca recibe el cliente |
| `PNA_CL_TCARTERA` | Alta Legacy usa constante técnica `1` | No editable | Efecto y catálogo requieren confirmación antes de tocarlo |
| `PNA_CL_REFPAGO` | Alta Legacy usa `2`; edición Legacy conserva/actualiza lógica relacionada | Fuera del formulario general | No cambiar sin alcance específico |
| `PAI_FL_CVE`, `PNA_NO_CODE`, `PNA_FG_FRONTERIZO`, `RFI_CL_CLAVE` | Participan en alta/edición Legacy | No editar en esta primera versión salvo contrato específico | Requieren reglas y catálogos autoritativos |
| `PNA_DS_EMAIL` | El flujo Legacy antiguo lo recibe, pero el alta moderna ya no crea correo | Fuera del contrato moderno | Administrar sólo en Correos |

No se ha confirmado mediante metadatos en esta revisión la existencia de
triggers, tablas de historial o restricciones adicionales de `CPERSONA`.
No deben asumirse por el uso de `PNA_FE_ULTMOD`.

## Matriz de CPFISICA

| Campo | Evidencia | Propuesta | Bloqueo |
|---|---|---|---|
| `PNA_FL_PERSONA` | Relaciona el subtipo con la persona | Inmutable | Confirmar FK o relación Legacy |
| `PFI_DS_NOMBRE` | `ActualizaPersona` lo actualiza | Editable | Confirmar longitud y nulabilidad |
| `PFI_DS_APATERNO` | `ActualizaPersona` lo actualiza; al menos un apellido se valida en pantalla | Editable con regla de al menos un apellido | Confirmar longitud y nulabilidad |
| `PFI_DS_AMATERNO` | `ActualizaPersona` lo actualiza | Editable opcional | Confirmar longitud y nulabilidad |
| `PFI_FE_NACIMIENTO` | La pantalla exige fecha válida y no futura | Editable con concurrencia compuesta | Confirmar tipo y precisión |
| `PFI_FE_ULTMOD` | `ActualizaPersona` la actualiza | Parte del token compuesto | Confirmar tipo físico |
| `USR_CL_CVE` | Lo escribe Legacy | Servidor/membresía | No exponer ni aceptar |
| Otros campos físicos | La fuente de alta deja varios valores en `NULL` | Fuera de esta HU | Requieren esquema y regla Legacy específica |

## Matriz de CPMORAL

| Campo | Evidencia | Propuesta | Bloqueo |
|---|---|---|---|
| `PNA_FL_PERSONA` | Relación usada por `ActualizaPersona` | Inmutable | Confirmar relación física |
| `PMO_DS_RAZON_SOCIAL` | `ActualizaPersona` lo actualiza | Editable | Confirmar longitud y nulabilidad |
| `PMO_DS_REGIMEN_CAPITAL` | Se actualiza como valor entero/textual en la fuente; el catálogo/regla moderna no está confirmado | No editable | Bloqueado hasta identificar catálogo y significado |
| `PMO_FE_CONSTITUCION` | Se actualiza junto con persona moral | Editable con fecha no futura | Confirmar tipo/precisión |
| `PMO_FE_ULTMOD` | Se actualiza en Legacy | Parte del token compuesto | Confirmar tipo físico |
| `USR_CL_CVE` | Lo escribe Legacy | Servidor/membresía | No exponer ni aceptar |
| Contacto, puesto, sector, actividad y demás campos | Aparecen en consultas Legacy, no en el contrato moderno de alta | Fuera de alcance | No inventar defaults ni reglas |

La personalidad no debe cambiarse entre física y moral en esta etapa. La
fuente Legacy registra `CPCAMBIO_PJ` y modifica el subtipo, pero no demuestra
que la conversión sea segura para todos los datos relacionados, contratos,
facturación o PLD.

## Roles en CPTIPO

`CPTIPO` se consulta actualmente por `PNA_FL_PERSONA` y `PTI_FG_VALOR`, con la
descripción activa del catálogo 5. La evidencia de alta y modificación Legacy
muestra una clave compuesta lógica/esperada por persona y código: el flujo
elimina todos los roles de la persona y vuelve a insertar la selección.

Decisiones propuestas para uCredit:

- `GET /api/v1/customers/{personId}/roles` devuelve códigos y descripciones
  activas, sin columnas internas.
- `PUT /api/v1/customers/{personId}/roles` recibe códigos únicos del catálogo
  5 activo; la sustitución es atómica.
- Debe existir al menos un rol; una lista vacía produce `400`.
- La validación de cada código se hace dentro de la transacción; no se
  aceptan roles inactivos, desconocidos o duplicados.
- La UI exige confirmación explícita antes de retirar roles existentes.
- La operación debe comparar el token de modificación de la persona y/o un
  token específico de roles antes de reemplazar la colección.

No se ha confirmado una FK física ni una restricción que impida eliminar el
último rol. Por eso la aplicación debe impedirlo y probarlo. No se corregirán
roles históricos masivamente. La lógica especial de aseguradora observada en
Legacy (`CASEGURADORA`) requiere una decisión separada antes de permitir
retirar o agregar ese rol.

## RFC, personalidad y estatus

### RFC

Legacy valida longitud/formato según personalidad y consulta duplicados. La
fuente moderna de alta usa bloqueo transaccional sobre `CPERSONA.PNA_CL_RFC`.
Aunque Legacy permite actualizarlo, no hay evidencia en esta etapa sobre el
impacto en contratos, facturación, PLD, CFDI, integraciones o historial. La
primera versión debe tratarlo como inmutable y devolver `400` si llega en una
solicitud de edición. Si el negocio exige cambio, se requiere una HU separada
con bloqueo concurrente, revisión de dependencias y auditoría específica.

### Personalidad jurídica

`CPERSONA.PNA_CL_PJURIDICA` selecciona `CPFISICA` o `CPMORAL`. Legacy registra
un cambio en `CPCAMBIO_PJ`, pero la evidencia no confirma migración completa de
datos entre subtipo, régimen fiscal, RFC, contratos o PLD. No se permitirá
cambiarla desde el contrato moderno inicial.

### Estatus

La consulta y el alta usan `PNA_FG_STATUS`; el catálogo 1 documentado contiene
`1 = ACTIVO` y `2 = INACTIVO` vigentes. Falta confirmar mediante código y
catálogos los efectos de desactivar una persona con contratos existentes,
domicilios, teléfonos, cuentas, correos o procesos PLD. Se propone:

- desactivación como acción explícita, con permiso `customers.write`;
- rechazo controlado si existen dependencias que Legacy considere bloqueantes;
- reactivación explícita y auditada;
- nunca borrar físicamente la persona.

No se debe inferir que cambiar el estatus cancela contratos o desactiva el
expediente completo.

## Endpoints y DTOs propuestos

### Datos generales

```text
GET /api/v1/customers/{personId}/general
PUT /api/v1/customers/{personId}/general
```

El GET requiere `customers.read`. El PUT requiere `customers.write`,
antiforgery, tenant seleccionado coincidente con `Deployment__TenantCode`,
`LegacyUserCode` de la membresía activa, entorno Development y las guardas de
conexión/base `pr_t` vigentes para escrituras.

El request inicial debe contener sólo campos aprobados y diferenciados por
personalidad:

```text
personId             ruta, no duplicado en el cuerpo
expectedModifiedAt   token técnico obligatorio
legalPersonality     sólo lectura en esta fase; rechazar cambios
rfc                  sólo lectura en esta fase; rechazar cambios
firstName            persona física
paternalSurname      persona física
maternalSurname      persona física
birthDate            persona física
legalName            persona moral
constitutionDate     persona moral
```

Los campos de país, grupo, riesgo, forma de contacto y régimen fiscal sólo se
incluyen después de confirmar sus catálogos, efectos y compatibilidad con una
edición. No se aceptan propiedades para luego ignorarlas. La respuesta debe
ser un DTO explícito y no incluir `USR_CL_CVE`, SQL, secretos ni campos Legacy
no aprobados. El token puede representarse como `modifiedAt` ISO 8601, sin
pretender que sea una fecha UTC si Legacy no lo garantiza.

### Roles

```text
GET /api/v1/customers/{personId}/roles
PUT /api/v1/customers/{personId}/roles
```

El request de reemplazo sería `{ "roleCodes": [ ... ], "expectedModifiedAt": ... }`.
Los códigos se validan contra el catálogo activo 5; la respuesta sólo incluye
`roleCode`, `roleName` y el token técnico. No se aceptan nombres, descripciones
ni columnas Legacy desde el navegador.

Respuestas controladas propuestas:

| Caso | HTTP | Código |
|---|---:|---|
| payload/catálogo inválido | 400 | `validation_error` |
| anónimo | 401 | estándar de autenticación |
| permiso/tenant no autorizado | 403 | estándar de autorización |
| persona o rol inexistente | 404 | `customer_not_found` / `role_not_found` |
| token vencido | 409 | `customer_modified` |
| dejar persona sin roles | 409 | `customer_role_required` |
| regla Legacy downstream | 409 | código específico pendiente |
| escritura no configurada/base no permitida | 503 | mensaje genérico |

## Transacción, auditoría y concurrencia

Cada mutación debe abrir una sola transacción Legacy. El flujo propuesto es:

1. autenticar, autorizar, validar tenant y resolver la membresía activa;
2. validar `LegacyUserCode` y guardas Development/`pr_t`;
3. cargar persona y token(es) con bloqueo apropiado;
4. validar catálogos, RFC y regla de mínimo un rol;
5. actualizar `CPERSONA` y el subtipo necesario;
6. reemplazar roles sólo dentro de la misma transacción;
7. escribir bitácora 4 para alta y 5 para modificación, sin PII;
8. confirmar; ante cualquier error, rollback completo.

La concurrencia debe usar, como mínimo, `CPERSONA.PNA_FE_ULTMOD` y la fecha de
modificación del subtipo (`PFI_FE_ULTMOD` o `PMO_FE_ULTMOD`) como token
compuesto. Para roles, si no existe una fecha propia confiable en `CPTIPO`, se
debe usar el token de persona y volver a leer la colección antes de confirmar.
Un UPDATE que no afecte filas debe producir `409 customer_modified`.

La actividad funcional 4/5 debe reutilizar el mecanismo ya empleado por
Customers. El formato de `BIT_DS_REFERENCIA` será técnico y sólo contendrá el
identificador de persona y la operación; nunca RFC, nombres, teléfonos,
correos, domicilio, roles completos, `LegacyUserCode` ni payload.

## Seguridad y privacidad

- `customers.read` protege las consultas; `customers.write` protege cada
  mutación.
- Tenant y `LegacyUserCode` se resuelven desde la membresía activa, nunca de
  headers o del cuerpo.
- Antiforgery se conserva en todas las mutaciones HTTP.
- Los errores no devuelven RFC, nombre, SQL, conexión ni valores capturados.
- No usar `localStorage`/`sessionStorage` para datos generales o roles.
- La API debe mantener `ProblemDetails` sin datos personales.
- No se ejecutarán SQL Server, POST, migraciones ni IdentityAdmin durante esta
  etapa de análisis.

## Pruebas propuestas

### Datos generales

- lectura autorizada, anónimo, permiso ausente, tenant inválido e inexistente;
- edición física válida y edición moral válida;
- campos no permitidos rechazados por JSON estricto;
- RFC, personalidad y campos inmutables rechazados;
- límites, nulabilidad y fechas confirmados contra `sys.columns`;
- token de persona y subtipo vencido produce `409 customer_modified`;
- error en subtipo o bitácora revierte `CPERSONA` y cualquier cambio previo;
- bitácora 5 sin PII;
- no se modifica domicilio, teléfono, cuenta, correo ni readiness.

### Roles

- lectura de roles activos y catálogo dinámico;
- mínimo un rol;
- varios roles;
- códigos desconocidos, inactivos y duplicados producen `400`;
- retirada confirmada de roles;
- último rol rechazado con `409 customer_role_required`;
- aseguradora y efectos `CASEGURADORA` cubiertos antes de habilitar esa opción;
- reemplazo atómico, rollback y concurrencia;
- respuestas sin columnas internas ni datos personales.

## Consultas de sólo lectura requeridas

Ejecutar sólo con autorización futura y sin valores personales; usar nombres
de tabla/columna confirmados y parámetros tipados cuando aplique.

### Esquema, defaults e índices

```sql
SELECT s.name AS SchemaName, t.name AS TableName, c.column_id,
       c.name AS ColumnName, ty.name AS SqlType, c.max_length,
       c.precision, c.scale, c.is_nullable, c.is_identity,
       dc.definition AS DefaultDefinition
FROM sys.tables AS t
JOIN sys.schemas AS s ON s.schema_id = t.schema_id
JOIN sys.columns AS c ON c.object_id = t.object_id
JOIN sys.types AS ty ON ty.user_type_id = c.user_type_id
LEFT JOIN sys.default_constraints AS dc ON dc.parent_object_id = c.object_id
  AND dc.parent_column_id = c.column_id
WHERE s.name = 'dbo'
  AND t.name IN ('CPERSONA', 'CPFISICA', 'CPMORAL', 'CPTIPO', 'CPCAMBIO_PJ')
ORDER BY t.name, c.column_id;
```

```sql
SELECT i.name AS IndexName, i.type_desc, i.is_unique, i.is_primary_key,
       i.is_unique_constraint, c.name AS ColumnName, ic.key_ordinal
FROM sys.tables AS t
JOIN sys.schemas AS s ON s.schema_id = t.schema_id
JOIN sys.indexes AS i ON i.object_id = t.object_id
JOIN sys.index_columns AS ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
JOIN sys.columns AS c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
WHERE s.name = 'dbo'
  AND t.name IN ('CPERSONA', 'CPFISICA', 'CPMORAL', 'CPTIPO', 'CPCAMBIO_PJ')
ORDER BY t.name, i.name, ic.key_ordinal;
```

### FK, triggers e historial

```sql
SELECT fk.name, OBJECT_NAME(fkc.parent_object_id) AS ChildTable,
       COL_NAME(fkc.parent_object_id, fkc.parent_column_id) AS ChildColumn,
       OBJECT_NAME(fkc.referenced_object_id) AS ParentTable,
       COL_NAME(fkc.referenced_object_id, fkc.referenced_column_id) AS ParentColumn
FROM sys.foreign_keys AS fk
JOIN sys.foreign_key_columns AS fkc ON fkc.constraint_object_id = fk.object_id
WHERE OBJECT_NAME(fkc.parent_object_id) IN ('CPERSONA','CPFISICA','CPMORAL','CPTIPO')
   OR OBJECT_NAME(fkc.referenced_object_id) IN ('CPERSONA','CPFISICA','CPMORAL','CPTIPO');
```

```sql
SELECT sch.name AS SchemaName, t.name AS TableName, tr.name AS TriggerName,
       tr.is_disabled, tr.is_instead_of_trigger
FROM sys.triggers AS tr
JOIN sys.tables AS t ON t.object_id = tr.parent_id
JOIN sys.schemas AS sch ON sch.schema_id = t.schema_id
WHERE sch.name = 'dbo'
  AND t.name IN ('CPERSONA','CPFISICA','CPMORAL','CPTIPO');
```

### Distribuciones sin PII

```sql
SELECT PNA_FG_STATUS, COUNT_BIG(*) AS Total
FROM dbo.CPERSONA
GROUP BY PNA_FG_STATUS;

SELECT PNA_CL_PJURIDICA, COUNT_BIG(*) AS Total
FROM dbo.CPERSONA
GROUP BY PNA_CL_PJURIDICA;

SELECT PTI_FG_VALOR, COUNT_BIG(*) AS Total
FROM dbo.CPTIPO
GROUP BY PTI_FG_VALOR;

SELECT PNA_FL_PERSONA, COUNT_BIG(*) AS RoleCount
FROM dbo.CPTIPO
GROUP BY PNA_FL_PERSONA
HAVING COUNT_BIG(*) = 0;
```

La última consulta no puede producir personas sin filas porque parte de
`CPTIPO`; para comprobar personas sin rol sin exponer identificadores se debe
usar sólo un conteo agregado:

```sql
SELECT COUNT_BIG(*) AS PersonsWithoutRole
FROM dbo.CPERSONA AS P
WHERE NOT EXISTS (SELECT 1 FROM dbo.CPTIPO AS T WHERE T.PNA_FL_PERSONA = P.PNA_FL_PERSONA);
```

También se requiere verificar, mediante conteos, contratos/relaciones por
persona antes de decidir los efectos de desactivar o cambiar RFC. No se deben
seleccionar nombres, RFC, correos, teléfonos, domicilios ni números de cuenta.

## Bloqueos pendientes

1. Confirmar tipos, longitudes, nulabilidad, defaults, índices, FK y triggers
   de las cinco tablas.
2. Confirmar catálogos y valores permitidos para grupo, riesgo, forma de
   contacto, país, régimen fiscal, estatus y régimen de capital.
3. Confirmar si `PNA_FE_ULTMOD`, `PFI_FE_ULTMOD` y `PMO_FE_ULTMOD` tienen la
   misma precisión y si el servidor Legacy usa hora local o UTC.
4. Determinar efectos reales de desactivar/reactivar una persona con
   contratos, PLD, facturación y relaciones existentes.
5. Decidir formalmente si RFC puede modificarse; la propuesta inicial lo deja
   inmutable.
6. Confirmar si `CPTIPO` tiene FK/trigger y el alcance de la lógica de
   aseguradora al quitar el rol 10.
7. Confirmar la actividad exacta y formato final de bitácora para edición
   moderna; `CommandArgumentControl` no basta como evidencia de actividad.
8. Confirmar que el futuro contrato de roles puede reemplazar la colección sin
   romper dependencias Legacy.

## Contrato aprobado e implementación inicial

Las reglas funcionales de esta etapa quedaron aprobadas. La implementación
moderna agrega:

```text
GET /api/v1/customers/{personId}/general
PUT /api/v1/customers/{personId}/general
```

El GET devuelve únicamente los datos necesarios para editar, los dos tokens de
concurrencia y los roles actuales. El PUT acepta sólo los campos editables de
la personalidad detectada y `roleCodes` opcional. Si `roleCodes` se omite, la
colección no cambia; si se envía, representa el conjunto final visible y se
valida en el servidor.

La implementación conserva roles históricos cuyo catálogo ya no está activo,
no agrega ni retira el rol 10 Aseguradora, exige al menos un rol activo y sólo
inserta o elimina roles permitidos. No modifica `CASEGURADORA`.

La edición usa una transacción única, vuelve a leer la persona con bloqueo,
compara `CPERSONA.PNA_FE_ULTMOD` y la fecha del subtipo, actualiza ambos
registros, aplica el delta de roles, registra actividad 5 sin PII y hace
commit. Cualquier discrepancia devuelve `409 customer_modified` y cualquier
error revierte la transacción.

La personalidad, RFC, estatus, país, grupo, riesgo, régimen fiscal, código y
fecha de alta se rechazan como campos de edición. El cambio de nombre o razón
social conserva la política PEP; contactos y roles no la ejecutan. La
dependencia directa con `CPERSONA_PEP` no se reintroduce.

La UI agrega “Datos generales”, diferencia física/moral, muestra los campos
inmutables como sólo lectura, permite roles dinámicos y confirma retiros. No
modifica ni recalcula las colecciones de domicilios, teléfonos, cuentas,
correos o el readiness.

La validación de esquema, catálogos, triggers, efectos del estatus y cobertura
de integración contra una base segura siguen siendo requisitos antes de
habilitar una prueba manual de escritura Legacy.
