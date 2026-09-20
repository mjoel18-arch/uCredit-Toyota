# Alta de clientes

## Estado

La investigación del alta Legacy quedó documentada. Esta etapa no implementa comandos ni ejecuta SQL de escritura.

Trazabilidad: `su_MtoPersona.aspx` -> `cmdGuardar_Click` -> `sn_clsPersona.ActualizaPersona` -> `sd_clsPersona.ActualizaPersona`.

El alta de persona usa una transacción para `CPERSONA`, `CPFISICA` o `CPMORAL` y `CPTIPO`. La bitácora de seguridad se guarda después del retorno exitoso mediante `Seguridad.Bitacora.Guarda`, en una operación separada.

## Matriz final de inserción

### Matriz autoritativa de `CPMORAL` y régimen fiscal

La evidencia obtenida directamente de `pr_t` mediante `sys.columns` es la fuente de verdad. `PMO_DS_REGIMEN_CAPITAL` y `CPERSONA.RFI_CL_CLAVE` no representan el mismo dato.

| Columna | Tipo SQL | Nullable | Propiedad propuesta | Tipo C# propuesto | Control UI | Parámetro Dapper | Validación |
|---|---|---:|---|---|---|---|---|
| `CPMORAL.PNA_FL_PERSONA` | `int` | No | `PersonId` | `int` | No editable | `DbType.Int32` | Consecutivo generado y FK a `CPERSONA` |
| `CPMORAL.PMO_DS_RAZON_SOCIAL` | `varchar(200)` | No | `LegalName` | `string` | Input requerido | `DbType.String`, 200 | Requerido y máximo 200 |
| `CPMORAL.PMO_FE_ULTMOD` | `datetime` | No | `OperationDate` | `DateTime` | No editable | `DbType.DateTime` | Fecha generada por el servidor |
| `CPMORAL.PMO_DS_REGIMEN_CAPITAL` | `int` | Sí | `CapitalRegime` | Pendiente | Select pendiente | Pendiente | Bloqueado hasta confirmar catálogo y regla funcional |

Las restantes columnas de `CPMORAL` son nullable según la evidencia: contacto, puesto, fecha de constitución, compañía social, tipo de proveedor, facturación, sector, actividad, subtipo de actividad, nacionalidad, clave tributaria y usuario Legacy. No se deben rellenar con valores estadísticos ni constantes no confirmadas.

#### Régimen fiscal: conceptos separados

| Elemento | Tipo real | Función |
|---|---|---|
| `CPERSONA.RFI_CL_CLAVE` | `int`, nullable | Valor almacenado por `CPERSONA`; no es la columna de texto del catálogo |
| `CREGIMEN_FISCAL.RFI_FL_CVE` | `tinyint`, `NOT NULL` | Identificador interno del catálogo |
| `CREGIMEN_FISCAL.RFI_CL_CLAVE` | `varchar(20)`, `NOT NULL` | Clave fiscal SAT consumida conceptualmente por el alta |
| `CREGIMEN_FISCAL.RFI_DS_DESCRIPCION` | `varchar(200)`, `NOT NULL` | Descripción presentada al usuario |
| `CREGIMEN_FISCAL.RFI_CL_PJURIDICA` | `tinyint`, `NOT NULL` | Compatibilidad con personalidad jurídica |
| `CREGIMEN_FISCAL.RFI_FG_STATUS` | `tinyint`, `NOT NULL` | Vigencia del régimen |

Implementado: el DTO HTTP y el formulario reciben `taxRegimeCode` como `string`; el repositorio consulta `CREGIMEN_FISCAL` con `RFI_CL_CLAVE = @TaxRegimeCode`, `RFI_FG_STATUS = 1` y la personalidad compatible. Sólo después de esa coincidencia activa se convierte la misma clave SAT a `int` para `CPERSONA.RFI_CL_CLAVE`; el parámetro del `INSERT` de `CPERSONA` es `DbType.Int32` porque esa columna es `int`. No se usa `RFI_FL_CVE` ni se envía texto directamente a `CPERSONA`.

Para la demostración, el frontend asigna temporalmente la clave SAT por personalidad y no muestra un campo fiscal editable: física (`1`) usa `605`, física con actividad empresarial (`2`) usa `612` y moral (`20`) usa `601`. Son valores predeterminados temporales, no una sustitución del catálogo; el backend siempre verifica vigencia y compatibilidad contra `CREGIMEN_FISCAL`.

Mientras no exista catálogo y regla funcional aprobada para `PMO_DS_REGIMEN_CAPITAL`, la validación rechaza el alta de personas morales antes de abrir una operación de escritura. No se asigna un valor arbitrario.

Las claves confirmadas en el catálogo incluyen `601`, `603`, `605`, `606`, `612`, `616`, `621`, `625` y `626`, entre otras. La compatibilidad se determina por `RFI_CL_PJURIDICA`, no por el texto visible ni por la frecuencia histórica.

### Matriz de `CDOMICILIO` confirmada

La evidencia de esquema confirma que las siguientes columnas son `NOT NULL` y no tienen default SQL utilizable por este alta. Las columnas nullable no se incluyen como obligación de entrada; cuando el formulario no las captura pueden conservar `NULL`.

| Columna | Permite NULL | Default SQL | Origen | Valor cuando no aplica |
|---|---:|---|---|---|
| `DMO_FL_CVE` | No | Ninguno | Consecutivo `CCATCONSEC` | Siempre se genera |
| `DMO_CL_CPOSTAL` | No | Ninguno | Formulario `PostalCode` | La validación rechaza vacío |
| `DMO_DS_NUMEXT` | No | Ninguno | Formulario `ExteriorNumber` | La validación rechaza vacío |
| `DMO_FG_TDIRECCION` | No | Ninguno | Formulario `AddressTypeCode` | La validación rechaza valores fuera de 1–4 |
| `DMO_DS_NUMINT` | No | Ninguno | Formulario `InteriorNumber` | `string.Empty` cuando no se captura, conforme al comportamiento Legacy |
| `DMO_FG_STATUS` | No | Ninguno | Formulario `AddressStatusCode` | La validación rechaza cero |
| `DMO_FG_FACTURA` | No | Ninguno | Regla del tipo de domicilio | `1` para tipos 1 y 2; `0` para los demás |
| `DMO_FG_EDOCTA` | No | Ninguno | Regla del tipo de domicilio | `1` para tipo 1; `0` para los demás |
| `DMO_FG_REGDEFAULT` | No | Ninguno | Regla de primer domicilio | `1` |
| `DMO_FG_OTROS` | No | `((0))` | Regla del tipo de domicilio | `1` para tipo 1; `0` para los demás |
| `DMO_FE_ULTMOD` | No | Ninguno | Fecha de operación | Fecha UTC de la operación |
| `PNA_FL_PERSONA` | No | Ninguno | Consecutivo de persona de la misma transacción | Siempre se genera |
| `PAI_FL_CVE` | No | Ninguno | `CountryCode` del formulario | La validación rechaza cero |

`DMO_DS_NUMINT` se envía siempre como parámetro Dapper `DbType.String` con tamaño 100. Los campos `State`, `City`, `Municipality` y `Neighborhood` se validan con el máximo confirmado de 70; `ExteriorNumber` con 100; `StreetAndNumber` con 200; `AddressReference` y `AddressSchedule` con 100.

La inserción se ejecuta por etapas separadas (`person`, `subtype`, `role`, `address`, `phone`, `email`, `email_usage`, `audit`, `commit`). Así, un error de compatibilidad en domicilio se registra como `Stage=address` y provoca rollback completo. Una `SqlException` de mapeo, como el error 515, no se clasifica como indisponibilidad 503: se propaga al manejador global como 500 con respuesta genérica.

| Campo | Valor inicial | Origen | Validación | Tabla destino | Efecto secundario | Evidencia |
|---|---|---|---|---|---|---|
| `PNA_FL_PERSONA` | Consecutivo | `sd_clsCatalogo.ObtenConsecutivo("CPERSONA")` | Consecutivo dentro de la transacción | `CPERSONA` | Identifica a la persona | `sdLsenet/sd_clsPersona.vb`, `ActualizaPersona` |
| `PNA_CL_PJURIDICA` | Selección | `cmbPerFiscal` | Catálogo de personalidad; distingue PF/PM | `CPERSONA` | Decide la extensión física o moral | `su_MtoPersona.aspx.vb`, `cmdGuardar_Click` |
| `PNA_CL_RFC` | Capturado o calculado | `txtRFC`; PF puede recalcularse con nombre y fecha | Validador Legacy y longitud según personalidad | `CPERSONA` | Participa en validaciones de duplicidad | `su_MtoPersona.aspx.vb`, `cmdGuardar_Click` |
| `PNA_DS_NOMBRE` | Composición Legacy | PF: apellidos y nombre; PM: razón social y régimen de capital | Nombre/apellidos o razón social obligatorios | `CPERSONA` | Resumen utilizado en búsquedas | `sdLsenet/sd_clsPersona.vb`, `ActualizaPersona` |
| `PNA_DS_EMAIL` | Valor resumido capturado | Campo de correo de la pantalla principal | No sustituye `CPERSONA_EMAIL` | `CPERSONA` | Conserva el resumen Legacy | `su_MtoPersona.aspx.vb` |
| `PNA_FG_FCONTACTO` | Selección | `cmbForCont` | Selección obligatoria | `CPERSONA` | Clasifica forma de contacto | `su_MtoPersona.aspx.vb` |
| `GPR_FL_CVE` | Selección | `cmbGpo` | Selección obligatoria | `CPERSONA` | Clasifica grupo | `su_MtoPersona.aspx.vb` |
| `GRI_FL_CVE` | Selección | `cmbRiesgo` | Selección obligatoria | `CPERSONA` | Clasifica riesgo | `su_MtoPersona.aspx.vb` |
| `PAI_FL_CVE` | Selección | `cmbPais` | Catálogo de países; afecta RFC | `CPERSONA` | Determina reglas de RFC | `su_MtoPersona.aspx.vb` |
| `PNA_FG_STATUS` | Selección | `cmbEst` | Catálogo de estatus | `CPERSONA` | Estado inicial | `su_MtoPersona.aspx.vb` |
| `PNA_CL_TCARTERA` | `1` | Constante del método | Asignación interna | `CPERSONA` | Clasifica cartera | `sd_clsPersona.vb`, `ActualizaPersona` |
| `PNA_CL_REFPAGO` | `2` | Constante del método | Asignación interna | `CPERSONA` | Referencia de pago por cliente | `sd_clsPersona.vb`, `ActualizaPersona` |
| `PNA_FE_ALTA` | Fecha de operación | `strFecOper` | Fecha enviada por la pantalla | `CPERSONA` | Fecha de negocio | `su_MtoPersona.aspx.vb`; `ActualizaPersona` |
| `PNA_FE_ULTMOD` | Reloj del servidor | Fecha actual Legacy | Fecha técnica, distinta de fecha de operación | `CPERSONA` | Auditoría técnica | `sd_clsPersona.vb`, `ActualizaPersona` |
| `PNA_NO_CODE` | `0` si está vacío; código capturado si aplica | `txtDealerCode` / `DealerCode` | Se rechaza duplicado; obligatorio para roles dealer, planta y sucursal | `CPERSONA` | Identifica código de dealer; no es PEP ni consecutivo | `su_MtoPersona.aspx(.vb)`; `ActualizaPersona` |
| Persona física | Nombre, apellidos y fecha; complementarios no capturados en `NULL` | Campos de PF | Nombre, al menos un apellido y fecha válida | `CPFISICA` | Extensión física | `su_MtoPersona.aspx.vb`; `ActualizaPersona` |
| Persona moral | Razón social, régimen de capital y fecha | Campos de PM | Razón social, RFC y fecha no futura | `CPMORAL` | Extensión moral | `su_MtoPersona.aspx.vb`; `ActualizaPersona` |
| Rol | Checkboxes seleccionados; `CLIENTE` es el rol funcional base | `tblTP` | Al menos un rol; roles dealer/planta/sucursal requieren código | `CPTIPO` | El rol aseguradora puede activar lógica adicional | `su_MtoPersona.aspx.vb`; `ActualizaPersona` |
| `DMO_FL_CVE` | Consecutivo | `ObtenConsecutivo("CDOMICILIO")` | Consecutivo de domicilio | `CDOMICILIO` | Relaciona domicilio y teléfonos | `sd_clsPersona.vb`, `ActualizaDomicilio` |
| `DMO_FG_TDIRECCION` | Selección | `cmbTipoDom` | Catálogo de tipos | `CDOMICILIO` | Determina flags disponibles | `su_MtoDireccion.aspx.vb`, `RevisarCboTD` |
| `DMO_FG_REGDEFAULT` | `1` para el primer domicilio; después selección | `chkDef` y regla del método | Al marcarlo desmarca los demás; un default no puede estar inactivo | `CDOMICILIO` | Principal por persona | `su_MtoDireccion.aspx.vb`; `ActualizaDomicilio` |
| `DMO_FG_STATUS` | Selección | Combo de estatus del domicilio | Default inactivo rechazado | `CDOMICILIO` | Activo/inactivo del domicilio | `su_MtoDireccion.aspx.vb` |
| `DMO_FG_FACTURA` | Tipo 1: `1`; tipo 2: `1`; tipos 3/4: `0`; otros dependen de selección | `RevisarCboTD` y checkboxes | La pantalla habilita o fija el checkbox | `CDOMICILIO` | Uso para facturación | `su_MtoDireccion.aspx.vb`, `RevisarCboTD` |
| `DMO_FG_EDOCTA` | Tipo 1: `1`; tipos 3/4: `0`; tipo 2 no quedó fijado en el fragmento revisado | `RevisarCboTD` y checkbox | Valida duplicidad de domicilio default de estado de cuenta | `CDOMICILIO` | Uso para estado de cuenta | `su_MtoDireccion.aspx.vb` |
| `DMO_FG_OTROS` | Tipo 1: `1`; tipos 3/4: `0`; otros dependen de selección | `RevisarCboTD` y checkbox | Se envía explícitamente | `CDOMICILIO` | Uso adicional | `su_MtoDireccion.aspx.vb` |
| `TFN_FL_CVE` | Consecutivo | `ObtenConsecutivo("CTELEFONO")` | Consecutivo de teléfono | `CTELEFONO` | Identificador telefónico | `sd_clsPersona.vb`, `ActualizaTelefono` |
| `TTL_FL_CVE` | Selección | `cmbTipoTel` | Catálogo de tipos de teléfono | `CTELEFONO` | Clasifica teléfono | `su_MtoTelefono.aspx.vb` |
| `TFN_FG_STATUS` | Selección | Combo de estatus | Catálogo de estatus | `CTELEFONO` | Activo/inactivo | `su_MtoTelefono.aspx.vb`; `ActualizaTelefono` |
| `TFN_FG_REGDEFAULT` | `1` para el primer teléfono; después selección | `chkDef` y regla del método | Al marcarlo desmarca los demás | `CTELEFONO` | Principal por persona | `sd_clsPersona.vb`, `ActualizaTelefono` |
| `MAI_FL_CVE` | Consecutivo | `ObtenConsecutivo("CPERSONA_EMAIL")` | Consecutivo de correo | `CPERSONA_EMAIL` | Identificador de correo | `Proleasenet.Negocio/sn_clsPersona.vb`, `InsertaActualizaPersona_Email` |
| `MAI_FG_STATUS` | Selección | `ddlStatus` | Nombre y correo obligatorios; formato validado | `CPERSONA_EMAIL` | Activo/inactivo | `su_MtoPersonaEmail.aspx.vb`, `cmbGuardar_Click` |
| `MAI_FG_OMITIR_ENVIO` | `0` | Constante del alta | No es selección del usuario | `CPERSONA_EMAIL` | El correo no queda omitido | `InsertaActualizaPersona_Email` |
| Uso de correo | Uno o varios usos marcados | Checkboxes de tipos de uso, catálogo 244 | Cada uso seleccionado se conserva | `KEMAIL_USO` | En actualización elimina usos previos y reinserta los elegidos | `su_MtoPersonaEmail.aspx.vb`; `InsertaActualizaPersona_Email` |
| `KEMAIL_USO.USR_CL_CVE` | Usuario de sesión | `strUser` / `LegacyUserCode` | Usuario requerido | `KEMAIL_USO` | Trazabilidad | `InsertaActualizaPersona_Email` |
| `KEMAIL_USO.USO_FE_MODIFICACION` | Fecha/hora del servidor | `GETDATE()` | Generada por Legacy | `KEMAIL_USO` | Fecha técnica del uso | `InsertaActualizaPersona_Email` |
| `BIT_FL_CVE` | Consecutivo | `ObtenConsecutivo("KBITACORA")` | Sólo si la acción está configurada para bitácora | `KBITACORA` | Auditoría | `Seguridad.vb`, `Bitacora.Guarda` |
| `ATV_FL_CVE` | `4` en alta; `5` en modificación | `CommandArgumentControl` de `cmdGuardar` | Consulta `KACCION.ATV_FG_BITACORA` | `KBITACORA` | Decide si se audita | `su_MtoPersona.aspx.vb`; `Seguridad.vb` |
| `BIT_FE_OPERACION` | Fecha de operación | `Utils.ObtenFechaOperacion()` | Fecha de negocio | `KBITACORA` | Auditoría funcional | `Seguridad.vb`, `Bitacora.Guarda` |
| `BIT_DS_REFERENCIA` | Texto de alta con la clave generada | Construido tras guardar; en alta identifica la acción y la clave | No debe ampliarse con datos personales | `KBITACORA` | Referencia humana | `su_MtoPersona.aspx.vb` |
| `BIT_TOP_CVE` | Vacío en este flujo | `TipoOperacion` no se asigna antes de `Guarda` | Columna no nula según esquema | `KBITACORA` | Conserva el valor Legacy del flujo | `Seguridad.vb`, `Bitacora.Guarda` |

## Reglas funcionales confirmadas

- `PNA_NO_CODE` es `DealerCode`: blanco se convierte en `0`, se valida que no esté asignado a otra persona y sólo es obligatorio para roles dealer, planta o sucursal.
- La integración PEP del alta queda desacoplada mediante `IPersonPepChecker`; el flujo moderno no consulta directamente ninguna tabla Legacy PEP ni registra un resultado negativo simulado. El proveedor representa `NoMatch`, `Match` o `Unavailable`. Una coincidencia exige `pepConfirmed=true` en una segunda solicitud explícita; sin confirmación devuelve 422 y `pep_confirmation_required`, mientras que `Unavailable` devuelve 503. En Development, `CustomerCreation__RequirePepCheck=false` permite la demostración, ignora `pepConfirmed` y expone el estado `NotExecuted`; esa configuración no está permitida en Production.
- El primer domicilio y el primer teléfono quedan predeterminados aunque el formulario no envíe la marca; una nueva selección default desmarca los existentes.
- Los usos de correo son múltiples. El alta crea una fila por uso seleccionado; la actualización elimina usos anteriores y los vuelve a crear.
- `CPERSONA_EMAIL` recibe la fecha de modificación proporcionada; `KEMAIL_USO` recibe la fecha/hora del servidor Legacy.
- `KEMAIL_USO` tiene FK física confirmada de `MAI_FL_CVE` a `CPERSONA_EMAIL`; no se confirmó PK física.
- La actividad `4` corresponde al alta desde `cmdGuardar`; la clase de bitácora sólo persiste si `KACCION` la habilita. La bitácora se escribe después de la operación de persona y no comparte esa transacción.

`IPersonPepChecker` se conserva como puerto para una futura integración PEP real. El alta no consulta tablas PLD/PEP ni simula un resultado negativo. `CustomerCreation__RequirePepCheck` es `true` por defecto; con `true` se requiere un proveedor real y su ausencia devuelve 503. Sólo Development puede usar `false`; en ese caso el alta informa `Validación PEP no ejecutada en este ambiente de demostración` y el resultado nunca se marca como validado.

## Clasificación de evidencia

- Restricciones físicas: PK/FK, nulabilidad e índices provienen del esquema entregado.
- Defaults SQL: no se consultaron `DEFAULT`; los valores indicados como constantes o reglas provienen del código, no de frecuencias.
- Comportamiento Legacy: los métodos y pantallas citados arriba.
- Propuesta pendiente: equivalencia moderna de la bitácora posterior y de `BIT_TOP_CVE` vacío.

## Bloqueos concretos

1. Confirmar/aprobar la equivalencia moderna para la auditoría posterior al alta y para `BIT_TOP_CVE` vacío.
2. Confirmar reglas de las banderas de domicilio para tipos 5, 6 y 7; los fragmentos revisados fijan explícitamente sólo tipos 1–4.
3. Definir si la validación/notificación PEP será requisito del nuevo alta; Legacy sólo muestra advertencia y notifica.

## Fuentes Legacy revisadas

La revisión fue sólo de lectura sobre `E:\Sitios IA Codex\Proleasenet_Toyota`. No se copiaron, movieron, editaron ni agregaron fuentes Legacy al repositorio público.

- `sdLsenet/sd_clsPersona.vb`: `ActualizaPersona`, `ActualizaDomicilio`, `ActualizaTelefono`.
- `snLsenet/sn_clsPersona.vb`: delegación de persona, domicilio y teléfono.
- `Proleasenet.Negocio/sn_clsPersona.vb`: `InsertaActualizaPersona_Email`.
- `Sitio Web/Migrado/su_MtoPersona.aspx` y `.aspx.vb`: controles, validaciones, `cmdGuardar_Click` y `CommandArgumentControl`.
- `Sitio Web/Migrado/su_MtoDireccion.aspx.vb`: reglas de domicilio y flags.
- `Sitio Web/su_MtoTelefono.aspx.vb`: estatus y default de teléfono.
- `Sitio Web/Catalogos/su_MtoPersonaEmail.aspx.vb`: validación, usos y estatus de correo.
- `snLsenet/sn_clsLavadoDinero.vb`: `ValidaPersonaPEP`.
- `Proleasenet.Seguridad/Seguridad.vb`: `Bitacora.Guarda`.

La documentación contiene reglas resumidas y referencias de archivo/método; no reproduce SQL ni código propietario.

No se ejecutaron SQL Server, escrituras Legacy, migraciones, IdentityAdmin, commit ni push.

## Decisiones aprobadas para la implementación moderna

- La v1 sólo permite domicilios de tipos 1 a 4. Los tipos 5 a 7 son rechazados por validación.
- El flujo moderno sólo crea el rol `CLIENTE`; no recibe `DealerCode` y fija `PNA_NO_CODE = 0`.
- La comprobación PEP ocurre antes de cualquier inserción. Una coincidencia exige confirmación explícita; ausencia de confirmación devuelve 422 y una falla del servicio devuelve 503. La notificación queda detrás de una interfaz separada y no participa en el commit.
- La escritura moderna de `KBITACORA` se realiza dentro de la misma transacción de cliente. Usa `ATV_FL_CVE = 4`, `BIT_TOP_CVE` vacío compatible con la columna, `BIT_FE_FECHA` generado por SQL Server, `LegacyUserCode` como actor y la referencia funcional Legacy sin RFC ni datos de contacto. Esta es una diferencia deliberada frente a Legacy, donde la bitácora se guarda después: si la auditoría moderna falla, todo el alta hace rollback.
- La conexión de escritura es distinta de la de lectura y está cerrada por defecto. Las pruebas de escritura sólo se habilitan en `Development`, con `UCREDIT_ALLOW_LEGACY_WRITE_TESTS=true`, `UCREDIT_LEGACY_WRITE_TEST_DATABASE=pr_t`, conexión separada y verificación efectiva de `DB_NAME() = pr_t` antes de abrir la transacción.
- `CCATCONSEC` se bloquea dentro de la transacción para obtener los consecutivos de persona, domicilio, teléfono, correo y bitácora. No se usa `MAX + 1`.
- Se añadió el contrato `POST /api/v1/customers`, permiso `customers.write`, DTO HTTP explícito, antiforgery y actor `LegacyUserCode`. La migración `AddLegacyUserCodeToUserTenantMembership` sólo se generó para revisión y no fue aplicada.

`IdentityAdmin` ahora aprovisiona idempotentemente `contracts.read`, `customers.read` y `customers.write` sólo en la membresía activa del tenant indicado, y carga `LegacyUserCode` desde `UCREDIT_BOOTSTRAP_LEGACY_USER_CODE`. La herramienta no se ejecutó. El código se valida contra la membresía del tenant activo y rechaza cambiar un valor existente distinto.
