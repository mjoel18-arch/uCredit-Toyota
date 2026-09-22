# Alta directa de contratos

Estado: análisis y diseño; no se implementan escrituras en esta etapa.

Este documento resume el flujo Legacy observado y separa la evidencia
confirmada de los puntos que aún requieren metadatos o una decisión funcional.
No autoriza POST, SQL de escritura, migraciones ni IdentityAdmin.

## Alcance actual

El módulo actual de contratos es de consulta. Sus repositorios consultan
KCONTRATO y tablas relacionadas; la amortización también es de lectura. No
existe todavía un comando moderno de alta. El futuro flujo debe recalcular en
servidor el expediente del cliente: persona activa, domicilio activo,
teléfono activo y cuenta activa. La respuesta previa del navegador no es
autoridad.

## Trazabilidad Legacy

Fuentes revisadas, sólo como referencia funcional:

- `E:\Sitios IA Codex\Proleasenet_Toyota\Sitio Web\Migrado\su_actContratoPropuesta.aspx`
- `E:\Sitios IA Codex\Proleasenet_Toyota\Sitio Web\Migrado\su_actContratoPropuesta.aspx.vb`
- `E:\Sitios IA Codex\Proleasenet_Toyota\snLsenet\sn_clsContratoPropuesta.vb`
- `E:\Sitios IA Codex\Proleasenet_Toyota\sdLsenet\sd_clsContratoPropuesta.vb`
- `E:\Sitios IA Codex\Proleasenet_Toyota\Proleasenet.Negocio\sn_clsContratos.vb`
- `E:\Sitios IA Codex\Proleasenet_Toyota\Proleasenet.Contratos\Contratos.vb`

La ruta solicitada `Proleasenet.Negocio\sn_clsContratoPropuesta.vb` no existe
en esa copia; la clase equivalente encontrada está en `snLsenet`.

La pantalla `su_actContratoPropuesta` prepara operación, persona, plaza,
moneda, producto y esquema. `cmdGuardar_Click` delega en `GuardaInfo`,
que ejecuta `ValidaCampos` y llama a `ActualizaContrato`. La capa de datos
inserta `KCONTRATO`, registra el cálculo de amortización y puede asociar una
propuesta. Cuando crea internamente la conexión usa una transacción; también
acepta una transacción recibida. La actividad exacta de auditoría aún no está
confirmada.

## Evidencia del formulario y validaciones

La pantalla tiene pestañas de Generales, Tasa, Tasa moratoria y Pagos finales.
Se observaron controles para operación, plaza, moneda, línea/producto,
periodicidad, plazo, fechas, capital, financiamiento, anticipo/enganche,
tasas, CAT, valor residual, opción de compra, CNBV, CFDI y dirección.

`ValidaCampos` exige, entre otros, plazo positivo, esquema de pago, fechas
válidas, primer pago para contrato directo, límites de importes y porcentajes,
base/puntos/factor de tasa ordinaria y moratoria, y reglas condicionadas por
el esquema de financiamiento. Los redondeos observados en Legacy no sustituyen
la precisión física que debe confirmarse en SQL.

La pantalla protege el doble envío del botón Guardar. La API futura debe
añadir idempotencia del lado servidor.

## Autorización versus bitácora

La pantalla asigna valores de `CommandArgumentControl` para acciones como
guardar, CFDI, dirección y tabla de amortización. Son controles de
autorización Legacy; no hay evidencia suficiente para interpretarlos como
`ATV_FL_CVE`. La implementación moderna debe reutilizar el mecanismo
validado de auditoría y no copiar esos números como actividades.

## Matriz de campos observados

| Concepto | Campo Legacy | Origen | Regla observada | Estado |
|---|---|---|---|---|
| Identificador | `CTO_FL_CVE` | Generado por Legacy | `strLlaveContrato`/ `strNoOper` se obtiene durante `ActualizaContrato` | Algoritmo y tipo físico pendientes |
| Persona | `PNA_FL_PERSONA` | Cliente seleccionado, servidor | No confiar en un valor manipulable del navegador | Relación confirmada por lectura; escritura pendiente |
| Operación | `TOP_CL_CVE` | Catálogo de operaciones | Debe estar activa y autorizada | Catálogo y alcance pendientes |
| Empresa | `EMP_FL_CVE` | Instalación/servidor | No aceptar libremente desde React | Regla de alcance pendiente |
| Plaza | `SUC_FL_CVE` | Selección autorizada | Legacy selecciona una plaza del usuario | Catálogo y autorización pendientes |
| Moneda | `CTO_CL_MONEDA` | Catálogo | Opción activa y compatible | Lectura existente; alta pendiente |
| Capital | `CTO_NO_CAPITAL` | Condición financiera | Decimal; precisión física pendiente | Campo observado |
| Financiamiento | `CTO_NO_MTO_FINANCIAR` | Cálculo servidor | No aceptar cálculo del navegador como autoridad | Campo observado |
| Anticipo | `CTO_NO_MTO_ANTICIPO`, `CTO_NO_PRC_ANTICIPO` | Esquema | Sólo si el esquema indica anticipo | Condición pendiente |
| Enganche | `CTO_NO_MTO_ENGANCHE`, `CTO_NO_PRC_ENGANCHE` | Esquema | Sólo si el esquema indica enganche | Condición pendiente |
| Plazo | `CTO_NO_PLAZO` | Formulario | Requerido y mayor que cero | Validación Legacy observada |
| Rentas | `CTO_NO_DEPRENTAS`, `CTO_NO_MTO_DEPRENTAS` | Esquema/formulario | Se redondean en Legacy | Precisión pendiente |
| IVA | `CTO_CL_IVA`/tasa IVA | Cliente/formulario | Legacy consulta si el cliente es fronterizo | Significado pendiente |
| Primer pago | `CTO_FE_PRIMER_PAGO` | Formulario/cálculo | Requerido en contrato directo | Regla observada |
| Desembolso | `CTO_FE_SOL_DESEMBOLSO` | Formulario/regla Legacy | Puede igualarse al primer pago si falta | Semántica pendiente |
| Tasa ordinaria | `CTO_NO_TASA_BASE`, `CTO_NO_PUNTOS_ADIC`, `CTO_NO_FACTOR`, `CTO_NO_TASA_NOMINAL` | Cálculo | Legacy valida base, puntos y factor | Fórmula/precisión pendientes |
| Tasa moratoria | `TAS_FL_CVEMORA`, `TLO_FL_CVEMORA`, `TAS_NO_BASEMORA`, `CTO_NO_PUNTOS_MORA`, `CTO_NO_FACTOR_MORA`, `CTO_NO_NOMINAL_MORA` | Catálogos/cálculo | Puede usar tasa ordinaria como base | Relaciones pendientes |
| Pago final | `CTO_NO_MTO_VRESIDUAL`, `CTO_NO_PRC_PAGOFINAL` | Esquema | Condicionado por el esquema | Precisión pendiente |
| Opción de compra | `CTO_FG_OPC`, `CTO_NO_MTO_OPCIONCOMPRA`, `CTO_NO_PRC_OPCIONCOMPRA` | Formulario | Monto/porcentaje con límites Legacy | Precisión pendiente |
| Estado | `CTO_FG_STATUS` | Servidor | Estado inicial y transiciones no confirmados | Bloqueado |
| Auditoría | `USR_CL_CVE`, `CTO_FE_ULTMOD` | Actor/reloj servidor | Nunca aceptar usuario del cliente | Precisión pendiente |
| Dirección | `DMO_FL_CVE`/`ddlDireccion` | Selección autorizada | No confundir con readiness | Relación pendiente |
| CNBV | `CNB_FL_CVE` | Catálogo/pantalla `su_catCNB` | Se envía a `ActualizaContrato` | Tabla y vigencia pendientes |
| CFDI | `UCO_CL_CLAVE`/`ddlUCFDI` | `CUSO_COMPROBANTES` | Tiene actualización separada | Destino y vigencia pendientes |

La consulta Legacy enumera más columnas de `KCONTRATO`, pero esa enumeración
no sustituye metadatos físicos de tipos, nulabilidad, claves, defaults y
triggers.

## Operación, propuesta y catálogos

El futuro catálogo `GET /api/v1/catalogs/contract-operation-types` debe leer
`KTOPERACION` y devolver sólo código y descripción activos, con orden
estable y sin duplicados ambiguos. La fuente usa `TOP_CL_CVE`,
`TOP_DS_DESCRIPCION`, `ESQ_CL_CVE` y filtros de `TOP_FG_STATUS = 1`;
tipos y alcance por empresa requieren confirmación.

La UI distingue propuesta y contrato directo mediante `intEsPropuesta`,
`intPropAsociada` y `conPropuesta`. Una propuesta autorizada no debe crear
automáticamente un contrato. La conversión posterior requiere acción
explícita, bloqueo de concurrencia y reglas aún no confirmadas.

CNBV, CFDI, plaza, empresa, periodicidad, calendario, exigibilidad, tasas,
seguro, esquema de amortización y formas de pago deben resolverse mediante
catálogos vigentes. No se deben hardcodear códigos ni asumir que una etiqueta
es una clave.

## API propuesta, no implementada

- `GET /api/v1/catalogs/contract-operation-types`
- `GET /api/v1/customers/{personId}/contract-readiness`
- `GET /api/v1/customers/{personId}/contract-addresses`
- `POST /api/v1/contracts`

Las consultas requieren autenticación, tenant seleccionado y alcance
autorizado. El POST requiere `contracts.write`, antiforgery, tenant
coincidente con `Deployment__TenantCode`, membresía activa y
`LegacyUserCode` resuelto desde esa membresía. La escritura local conserva
las guardas de Development y base efectiva `pr_t`.

El navegador no debe enviar como autoridad tenant, empresa, estado, usuario,
permisos ni cálculos derivados. El alcance de empresa debe resolverse en
servidor; no se debe copiar automáticamente el filtro de Customers ni
inventar un filtro para clientes.

## Transacción, idempotencia y respuestas

El caso de uso futuro debe autorizar, resolver actor/tenant, revalidar
readiness, validar catálogos, generar el identificador, escribir
`KCONTRATO` y relaciones requeridas, generar o asociar amortización sólo
con reglas confirmadas, auditar dentro de la transacción y confirmar una sola
vez. Un error debe revertir contrato, consecutivos, relaciones, amortización
y auditoría. La estrategia de idempotencia aún requiere decisión.

Respuestas previstas: `201` creado; `400` payload/catálogo/escala inválida;
`401` anónimo; `403` permiso, tenant o alcance inválido; `404` persona,
propuesta o referencia inexistente; `409` idempotencia, consecutivo,
propuesta o concurrencia; `422` readiness incompleto o regla de negocio;
`503` escritura no configurada o guardas de prueba incumplidas. Ningún
ProblemDetails debe revelar SQL, PII, importes, tasas o secretos.

## Seguridad, privacidad y UX

La futura UI debe ser un wizard responsive y accesible: cliente/operación,
datos generales, condiciones financieras y revisión. Debe mostrar readiness,
bloquear la captura si falta domicilio, teléfono o cuenta activa, prevenir
doble envío y refrescar el detalle después de `201`. No usará
`localStorage` ni `sessionStorage` para PII.

Los logs sólo pueden contener etapa, tipo de excepción, número SQL cuando
corresponda y correlation ID. Nunca deben contener nombre, RFC, contrato,
importes, tasas, domicilios, datos bancarios, `LegacyUserCode`, payload ni
cadena de conexión.

## Consultas de sólo lectura necesarias

Antes de implementar se requieren consultas de metadatos y agregados, sin
identificadores personales ni valores financieros:

```sql
SELECT c.name, t.name AS type_name, c.max_length, c.precision, c.scale,
       c.is_nullable, dc.definition AS default_definition
FROM sys.columns AS c
JOIN sys.tables AS tb ON tb.object_id = c.object_id
JOIN sys.types AS t ON t.user_type_id = c.user_type_id
LEFT JOIN sys.default_constraints AS dc ON dc.parent_object_id = c.object_id
  AND dc.parent_column_id = c.column_id
WHERE SCHEMA_NAME(tb.schema_id) = 'dbo' AND tb.name = 'KCONTRATO';

SELECT i.name, i.is_unique, i.is_primary_key, ic.key_ordinal,
       col.name AS column_name
FROM sys.indexes AS i
JOIN sys.index_columns AS ic ON ic.object_id = i.object_id
  AND ic.index_id = i.index_id
JOIN sys.columns AS col ON col.object_id = ic.object_id
  AND col.column_id = ic.column_id
WHERE i.object_id = OBJECT_ID('dbo.KCONTRATO');

SELECT fk.name, parent_col.name AS child_column,
       ref_col.name AS parent_column, ref_tab.name AS parent_table
FROM sys.foreign_keys AS fk
JOIN sys.foreign_key_columns AS fkc ON fkc.constraint_object_id = fk.object_id
JOIN sys.tables AS parent_tab ON parent_tab.object_id = fk.parent_object_id
JOIN sys.tables AS ref_tab ON ref_tab.object_id = fk.referenced_object_id
JOIN sys.columns AS parent_col ON parent_col.object_id = fk.parent_object_id
  AND parent_col.column_id = fkc.parent_column_id
JOIN sys.columns AS ref_col ON ref_col.object_id = fk.referenced_object_id
  AND ref_col.column_id = fkc.referenced_column_id
WHERE parent_tab.object_id = OBJECT_ID('dbo.KCONTRATO');

SELECT name, is_disabled FROM sys.triggers
WHERE parent_id = OBJECT_ID('dbo.KCONTRATO');

SELECT TOP (100) TOP_CL_CVE, TOP_FG_STATUS, COUNT_BIG(*) AS total
FROM dbo.KTOPERACION
GROUP BY TOP_CL_CVE, TOP_FG_STATUS;

SELECT CTO_FG_STATUS, TOP_CL_CVE, CTO_CL_MONEDA, COUNT_BIG(*) AS total
FROM dbo.KCONTRATO
GROUP BY CTO_FG_STATUS, TOP_CL_CVE, CTO_CL_MONEDA;
```

También faltan metadatos y vigencia de CNBV, CFDI, plaza, empresa, esquema,
tasas, calendario y amortización, además del mecanismo de consecutivos y la
actividad de bitácora.

## Pruebas previstas

- autorización `401`, `403`, tenant inválido y permiso ausente;
- catálogo de operaciones activo, ordenado y sin duplicados ambiguos;
- persona inexistente, cliente inactivo y readiness incompleto;
- validación de fechas, escalas, límites, moneda y combinaciones financieras;
- propuesta autorizada versus alta directa;
- idempotencia y doble envío;
- parámetros tipados, sin concatenación y sin `NOLOCK`;
- rollback después de cada tabla relacionada y de auditoría;
- concurrencia de consecutivos y conversión de propuestas;
- precisión decimal, redondeos y fechas ISO;
- amortización sólo después de confirmar sus reglas;
- logs, ProblemDetails y JSON sin PII ni secretos;
- pruebas de arquitectura contra dependencias indebidas.

## Bloqueos y decisiones pendientes

1. Falta completar el esquema físico de las columnas restantes de
   `KCONTRATO`: tipos, nulabilidad, defaults, índices y columnas obligatorias.
   Ya están confirmados su PK, FK de persona, ausencia de triggers y ausencia
   de CHECK.
2. Algoritmo y consecutivo de `CTO_FL_CVE`/`CTO_NO_CONSECUTIVO`.
3. Operaciones elegibles y relación con empresa, plaza, producto y esquema.
4. Tabla, clave, vigencia y obligatoriedad de CNBV y CFDI.
5. Relación exacta entre `ddlDireccion`, `DMO_FL_CVE` y contrato.
6. Reglas físicas y funcionales de IVA, tasas, moratoria, seguro, calendario,
   exigibilidad, pagos finales y opción de compra.
7. Estado inicial y transiciones de contrato directo.
8. Frontera real de la transacción Legacy, amortización, referencias y
   auditoría.
9. Conversión única y concurrente de propuesta autorizada.
10. Mecanismo de idempotencia y alcance de empresa.

Hasta cerrar estos puntos no se debe implementar `POST /api/v1/contracts` ni
ejecutar escrituras contra Legacy.

## Addendum: evidencia física de KCONTRATO

La evidencia recibida confirma:

- KCONTRATO tiene 100 columnas.
- CTO_FL_CVE es varchar(15) y PK.
- KCONTRATO no usa CCATCONSEC confirmado.
- Existe dbo.spLsnetGeneraClaveContrato; su contrato, parámetros y relación
  con CTO_NO_CONSECUTIVO aún deben caracterizarse.
- No existen triggers ni restricciones CHECK.
- PNA_FL_PERSONA es NOT NULL y tiene FK física a CPERSONA.
- TOP_CL_CVE es nullable físicamente, pero obligatorio funcionalmente.
- DMO_FL_CVE es nullable.
- CNB_FL_CVE es int NOT NULL.
- UCO_CL_CLAVE es varchar(20) nullable.
- CTO_FE_SOL_DESEMBOLSO es datetime NOT NULL.
- KPROPUESTA sí tiene consecutivo, pero propuestas siguen fuera de alcance.

La falta de triggers y CHECK no sustituye las validaciones de aplicación.
Tampoco es seguro un INSERT mínimo hasta identificar el origen de todas las
columnas NOT NULL.

## Matriz exhaustiva del método ActualizaContrato

La matriz está basada en la firma y el INSERT observados en:

- sdLsenet/sd_clsContratoPropuesta.vb, método ActualizaContrato.
- Sitio Web/Migrado/su_actContratoPropuesta.aspx.vb, método GuardaInfo.

La clasificación usa únicamente: capturada por usuario, derivada, constante
confirmada, generada por procedimiento o sin regla confirmada. Un literal
observado en SQL Legacy no se traslada automáticamente como regla moderna.

| Parámetro o valor | Columna o efecto | Origen/control | Clasificación | Catálogo/validación | Obligación | Tabla adicional |
|---|---|---|---|---|---|---|
| strContrato/strLlaveContrato | CTO_FL_CVE | Generador Legacy | Generada por procedimiento | dbo.spLsnetGeneraClaveContrato | PK | Relaciones de cálculo |
| intPersona | PNA_FL_PERSONA | Cliente seleccionado | Derivada | FK a CPERSONA | NOT NULL | Bitácora |
| intContConsec | CTO_NO_CONSECUTIVO | Parámetro Legacy | Sin regla confirmada | CCATCONSEC no confirmado | Pendiente | Ninguna confirmada |
| strTipoOper | TOP_CL_CVE | Selector de operación | Capturada por usuario | KTOPERACION y estado activo | Nullable físico; obligatoria funcional | Producto/esquema |
| intEmpresa | EMP_FL_CVE | Instalación/contexto | Derivada o sin regla confirmada | Alcance pendiente | Pendiente | Producto/plaza |
| intPlaza | SUC_FL_CVE | Plaza Legacy | Capturada por usuario | Catálogo pendiente | Pendiente | Ninguna confirmada |
| intTasa | TAS_FL_CVE | Selector de tasa | Capturada por usuario | Catálogo pendiente | Pendiente | Ninguna confirmada |
| intPropuesta | KPR_FL_CVE | Propuesta asociada | Capturada/derivada | Fuera de alcance | Pendiente | KCONTRATO_CALCULO_TAMORTIZACION |
| intLineaCred | LCR_FL_CVE | Línea de crédito | Capturada por usuario | Catálogo pendiente | Pendiente | Ninguna confirmada |
| intPaquete/intOpcPaq | KPQ_FL_CVE/OPP_FL_CVE | Paquete y opción | Capturada por usuario | Catálogo pendiente | Pendiente | Producto |
| intProducto | PRD_FL_CVE | Producto | Capturada por usuario | Catálogo pendiente | Pendiente | Facturación |
| intStatCont | CTO_FG_STATUS | Flujo de estado | Sin regla confirmada | Catálogo pendiente | Pendiente | Ninguna confirmada |
| intMoneda | CTO_CL_MONEDA | Selector moneda | Capturada por usuario | Moneda activa pendiente | Pendiente | CMONEDA |
| dblPCapital | CTO_NO_CAPITAL | Condición financiera | Capturada/derivada | Escala física pendiente | Pendiente | Ninguna confirmada |
| dblMtoFinanciar | CTO_NO_MTO_FINANCIAR | Cálculo financiero | Derivada | Fórmula y escala pendientes | Pendiente | Amortización |
| dblMtoAnt/dblPtjAnt | CTO_NO_MTO_ANTICIPO/CTO_NO_PRC_ANTICIPO | Esquema | Derivada | ESQ_FG_ANT_RENTA | Condicional | Ninguna confirmada |
| dblMtoAnt/dblPtjAnt | CTO_NO_MTO_ENGANCHE/CTO_NO_PRC_ENGANCHE | Esquema | Derivada | ESQ_FG_ENGANCHE | Condicional | Ninguna confirmada |
| intPlazo | CTO_NO_PLAZO | Campo plazo | Capturada por usuario | ValidaCampos, mayor que cero | Confirmada en Legacy | Ninguna confirmada |
| dblNoRent/dblMtoRent | CTO_NO_DEPRENTAS/CTO_NO_MTO_DEPRENTAS | Esquema/formulario | Capturada/derivada | Escala pendiente | Pendiente | Amortización |
| dblTasaIVA | CTO_CL_IVA según INSERT observado | Cliente/formulario | Derivada/capturada | IvaClienteFronterizo | Pendiente | Ninguna confirmada |
| dblDeposito | CTO_NO_MTO_DEPOSITO | Formulario | Capturada/derivada | Escala pendiente | Pendiente | Ninguna confirmada |
| intCNB | CNB_FL_CVE | Control CNBV | Capturada por usuario | su_catCNB, vigencia pendiente | NOT NULL | Ninguna confirmada |
| intEdoEquipo | CTO_CL_EDOEQUIPO | Estado de equipo | Capturada por usuario | Catálogo pendiente | Pendiente | Ninguna confirmada |
| dblTasaBase/dblPuntos/dblFactor/dblTasaNom | CTO_NO_TASA_BASE/CTO_NO_PUNTOS_ADIC/CTO_NO_FACTOR/CTO_NO_TASA_NOMINAL | Pestaña Tasa | Capturada/derivada | ValidaCampos | Pendiente | Ninguna confirmada |
| strFecPrimerPago | CTO_FE_PRIMER_PAGO | Fecha formulario/cálculo | Capturada/derivada | Validación de fechas | Pendiente | Amortización |
| strFecSolDesem | CTO_FE_SOL_DESEMBOLSO | Fecha desembolso | Capturada/derivada | Regla Legacy de fechas | NOT NULL | Desembolso |
| strFecActivacion | CTO_FE_ACTIVACION | Estado | Derivada | Estado pendiente | Pendiente | Ninguna confirmada |
| intFormaPagoSeg | CTO_CL_FPAGO_SEGBIEN | Seguro | Capturada por usuario | Catálogo pendiente | Pendiente | Ninguna confirmada |
| strFecBaja | CTO_FE_BAJA | Estado | Derivada | Estado pendiente | Pendiente | Ninguna confirmada |
| strFecFirmaCont/strFecFirmaAnexo | CTO_FE_FIRMA_CONTRATO/CTO_FE_FIRMAANEXO | Formulario | Capturada por usuario | Fechas Legacy | Pendiente | Ninguna confirmada |
| intFgCesionado/intFgReestructura | CTO_FG_CESION/CTO_FG_REESTRUCTURA | Flujo Legacy | Derivada/constante sin confirmar | Reestructura pendiente | Pendiente | Reestructura |
| intExigibilidad/intCalendario | CTO_CL_EXIGIBILIDAD/CTO_CL_CALENDARIO | Catálogos | Capturada por usuario | Catálogos pendientes | Pendiente | Amortización |
| intPeriodicidad/intEsqPago | CTO_CL_FPAGO/PPG_FL_CVE/CTO_CL_ESQPAGO | Esquema pago | Capturada/derivada | Catálogos pendientes | Pendiente | Amortización |
| intAplicaSegVida | CTO_FG_SEGVIDA | Seguro | Capturada por usuario | Catálogo pendiente | Pendiente | Ninguna confirmada |
| dblMtoValRes/dblPtjValRes | CTO_NO_MTO_VRESIDUAL/CTO_NO_PRC_VRESIDUAL | Pagos finales | Capturada/derivada | ESQ_FG_APLICA_VRESIDUAL | Condicional | Amortización |
| intCveAmor | CTO_CL_AMORT | Esquema | Capturada por usuario | Catálogo pendiente | Pendiente | Amortización |
| intTipoCalc | TLO_FL_CVE | Tipo cálculo | Capturada por usuario | Catálogo pendiente | Pendiente | Amortización |
| strComposicion | CTO_DS_COMPOSICION | Esquema/formulario | Capturada/derivada | Regla pendiente | Pendiente | Ninguna confirmada |
| intAplicaTasaReg/dblValTasaTecho/dblValTasaPiso | CTO_FG_TASA_REGULADA/CTO_NO_TASA_TECHO/CTO_NO_TASA_PISO | Pestaña Tasa | Capturada/derivada | Regla regulada | Condicional | Ninguna confirmada |
| intTasaMora/intTipoCalcMora/dblTasaBaseMora/dblPuntosMora/dblFactorMora/dblTasaNomMora | TAS_FL_CVEMORA/TLO_FL_CVEMORA/TAS_NO_BASEMORA/CTO_NO_PUNTOS_MORA/CTO_NO_FACTOR_MORA/CTO_NO_NOMINAL_MORA | Pestaña moratoria | Capturada/derivada | Catálogos de tasas | Condicional | Ninguna confirmada |
| intFgOCompra/dblMtoOpcCompra/dblPrjOpcCompra | CTO_FG_OPC/CTO_NO_MTO_OPCIONCOMPRA/CTO_NO_PRC_OPCIONCOMPRA | Pagos finales | Capturada/derivada | Regla de opción pendiente | Condicional | Ninguna confirmada |
| dblMtoValRes/dblPtjValRes | CTO_NO_MTO_PAGOFINAL/CTO_NO_PRC_PAGOFINAL | Pagos finales | Derivada | ESQ_FG_APLICA_PFINAL | Condicional | Amortización |
| intEsContMtro/strCveContMtro | CTO_FG_MAESTRO/CTO_FL_CVE_MAESTRO | Flujo Legacy | Derivada | Regla pendiente | Pendiente | Ninguna confirmada |
| intAnexo | CTO_NO_ANEXO | Flujo Legacy | Derivada | Regla pendiente | Pendiente | Ninguna confirmada |
| dblSaldo | CTO_NO_SALDO | Inicialización/cálculo | Derivada | Regla de saldo pendiente | Pendiente | Amortización |
| strFecOper | CTO_FE_ULTMOD y fechas | Reloj servidor | Derivada | Fecha de operación | No aceptar cliente | Bitácora |
| strUser | USR_CL_CVE | LegacyUserCode de membresía | Derivada | Membresía activa | No aceptar cliente | Bitácora |
| bUsaTasaOrdinaria | CTO_FG_TASA_USAORDINARIA | Pestaña moratoria | Capturada por usuario | Regla pendiente | Condicional | Ninguna confirmada |
| strCveUso | UCO_CL_CLAVE | Selector CFDI | Capturada por usuario | CUSO_COMPROBANTES | Nullable; vigencia pendiente | ActualizaUsoCFDI |
| intDireccion | DMO_FL_CVE | Selector dirección | Capturada por usuario | Dirección de contrato pendiente | Nullable | ActualizaUsoDireccion |
| FlagIVAFronterizo | CTO_FG_FRONTERIZO | Cálculo de persona | Derivada | IvaClienteFronterizo | Pendiente | Ninguna confirmada |
| strEstatusFact | CTO_FG_STATUS_FACTURACION | Facturación | Capturada/derivada | Catálogo pendiente | Pendiente | Facturación |
| Monto_EVM/Porcentaje_EVM | CTO_NO_EVM/CTO_NO_PORC_EVM | Formulario/configuración | Capturada/derivada | Regla pendiente | Pendiente | Ninguna confirmada |
| literales 0, 1, NULL y GETDATE | Varias columnas del INSERT | SQL Legacy | Constante confirmada sólo para esa ruta | No generalizar | Columna por columna pendiente | Varias relaciones |

La matriz cubre los parámetros y asignaciones identificables en la firma y el
INSERT revisados. Las columnas restantes de las 100 no están demostradas por
este método y requieren comparar el INSERT efectivo con sys.columns.

## Columnas NOT NULL sin regla confirmada

La evidencia física completa confirma 100 columnas: 70 NOT NULL y 30
nullable. La matriz exhaustiva de las 70 columnas se encuentra en el
addendum de clasificación al final de este documento.

## Addendum: autoridad de operación y generación de clave

La evidencia adicional corrige la descripción del catálogo de operaciones:

- KTOPERACION es una vista, no una tabla física.
- KTOPERACION_ORIGINAL conserva la estructura física subyacente.
- La clave de contrato debe generarse exclusivamente mediante
  dbo.spLsnetGeneraClaveContrato.
- No se debe reproducir el algoritmo de esa rutina en C#.
- La rutina debe ejecutarse dentro de la misma transacción que el alta.
- Según CPARAMETRO 19/142, la rutina puede actualizar
  KTOPERACION.TOP_NO_CONSECUTIVO o CCATCONSEC.
- CPARAMETRO 215 modifica el formato de la clave.
- La rutina usa CFECHA_OPERACION como parte de su lógica.
- CTO_FL_CVE está limitado a 15 caracteres.

La ausencia de uso directo confirmado de CCATCONSEC en la inserción no
permite sustituir el procedimiento: el procedimiento es la autoridad y puede
seleccionar el mecanismo de consecutivo según configuración.

## CNBV, CFDI y dirección de contrato

- CNBV se obtiene de CCNB y su selección está relacionada con TOP_CL_CVE.
  El código, vigencia y compatibilidad exactos siguen requiriendo validación.
- El uso CFDI se valida mediante el régimen fiscal y
  KRELACION_REGIMEN_USOCFDI. No basta validar sólo la descripción de
  CUSO_COMPROBANTES.
- Si se selecciona DMO_FL_CVE, debe pertenecer al cliente y estar activo.
  La aplicación no debe aceptar una dirección de otra persona ni una
  dirección inactiva.
- DMO_FL_CVE es nullable en KCONTRATO; nullable no significa que cualquier
  dirección o valor enviado sea válido.

KCONTRATO no tiene triggers ni restricciones CHECK. La aplicación debe validar
integridad, catálogos, pertenencia, estados y compatibilidades, y ejecutar
rollback ante cualquier error.

## Inventario de las 100 columnas de KCONTRATO

El inventario siguiente reúne las columnas de la proyección Legacy observada y
las columnas del INSERT de ActualizaContrato. Se conserva el nombre Legacy
para hacer trazable la comparación; no constituye un DTO público.

CTO_FL_CVE, CTO_NO_CONSECUTIVO, TOP_CL_CVE, EMP_FL_CVE, TAS_FL_CVE,
KPR_FL_CVE, KPQ_FL_CVE, LCR_FL_CVE, CTO_FE_GENERACION, OPP_FL_CVE,
CPB_FL_CVE, CTO_FE_INICIO, PRD_FL_CVE, CTO_NO_CAPITAL,
CTO_DS_CALIFICACION, CTO_FG_REESTRUCTURA, CTO_FG_STATUS, CTO_CL_MONEDA,
CTO_NO_MTO_FINANCIAR, CTO_NO_MTO_ANTICIPO, CTO_NO_PRC_ANTICIPO,
CTO_NO_PLAZO, CTO_NO_MTO_ENGANCHE, CTO_NO_PRC_ENGANCHE,
CTO_NO_DEPRENTAS, CTO_NO_MTO_DEPRENTAS, CTO_CL_IVA, CTO_NO_MTO_DEPOSITO,
CNB_FL_CVE, CTO_CL_EDOEQUIPO, CTO_NO_TASA_BASE, CTO_NO_PUNTOS_ADIC,
CTO_NO_FACTOR, CTO_FE_PRIMER_PAGO, CTO_FE_ULTPAGO,
CTO_FE_SOL_DESEMBOLSO, CTO_FE_ACTIVACION, CTO_NO_TASA_NOMINAL,
CTO_CL_FPAGO_SEGBIEN, CTO_FE_BAJA, CTO_FE_FIRMA_CONTRATO,
CTO_FE_FIRMAANEXO, CTO_CL_EXIGIBILIDAD, CTO_CL_CALENDARIO, CTO_CL_FPAGO,
PPG_FL_CVE, CTO_FG_SEGVIDA, CTO_NO_MTO_VRESIDUAL, CTO_CL_ESQPAGO,
CTO_NO_PRC_VRESIDUAL, CTO_NO_VENACTUAL, CTO_NO_SALDO, CTO_CL_AMORT,
TLO_FL_CVE, CTO_FE_ULTMOD, USR_CL_CVE, CTO_DS_COMPOSICION,
CTO_FG_TASA_REGULADA, CTO_NO_TASA_TECHO, CTO_NO_TASA_PISO, TAS_FL_CVEMORA,
TLO_FL_CVEMORA, TAS_NO_BASEMORA, CTO_NO_PUNTOS_MORA, CTO_NO_FACTOR_MORA,
CTO_NO_NOMINAL_MORA, CTO_NO_MTO_OPCIONCOMPRA, CTO_NO_PRC_OPCIONCOMPRA,
CTO_NO_TIR, CTO_NO_PORC_CAT, CTO_NO_MTO_PAGOFINAL, CTO_NO_PRC_PAGOFINAL,
CTO_FG_CHKLIST, CTO_FG_MAESTRO, CTO_FL_CVE_MAESTRO, CTO_NO_ANEXO,
CTO_FE_ULTCALC_INTERES, CTO_NO_GRACIA_INT, CTO_FG_CESION, CTO_FG_CV,
SUC_FL_CVE, CTO_FG_OPC, PNA_FL_PERSONA, SCB_FL_CVE,
CTO_FG_TASA_USAORDINARIA, CTO_NO_SDOANT, EQI_FL_CVE, CEC_CL_ESQPAGO,
STC_FL_CVE, APP_FL_CVE, DMO_FL_CVE, UCO_CL_CLAVE, CTO_FG_FRONTERIZO,
CTO_FG_STATUS_FACTURACION, CTO_FE_OPTERMINA, USR_CL_OPTERMINA, CTO_NO_EVM,
CTO_NO_PORC_EVM.

La fuente SQL consultada contiene la proyección Legacy y el INSERT observado;
la numeración física de columnas debe prevalecer si difiere del orden de esa
proyección.

## Matriz de columnas NOT NULL

La evidencia recibida confirma la nulabilidad de tres columnas, pero no
entrega el listado de nulabilidad de las 100 columnas. Por ello sólo se
pueden clasificar con certeza las siguientes:

| Columna | Origen | Regla | Valor confirmado | Pestaña/control | Captura o derivación | Evidencia | Bloqueo |
|---|---|---|---|---|---|---|---|
| PNA_FL_PERSONA | Cliente seleccionado | Debe existir en CPERSONA y conservar FK | PersonId validado por servidor | Cliente | Derivada | FK física confirmada y llamada ActualizaContrato | Sin bloqueo de origen |
| CNB_FL_CVE | Catálogo CNBV | Debe ser un valor válido de CCNB compatible con TOP_CL_CVE | Tipo int NOT NULL | CNBV | Capturada | intCNB de GuardaInfo y relación CCNB/TOP_CL_CVE confirmada | Catálogo, vigencia y default pendientes |
| CTO_FE_SOL_DESEMBOLSO | Fechas del contrato | Debe existir y respetar reglas de contrato directo | datetime NOT NULL | Generales/desembolso | Capturada o derivada | strFecSolDesem y ValidaCampos | Origen exacto y default pendiente |

Para las otras columnas del inventario no se recibió nulabilidad individual.
No es válido marcarlas como NOT NULL, nullable o satisfechas sólo porque el
INSERT Legacy les asigne un literal.

### Conteo solicitado

- Total exacto de columnas NOT NULL: 70.
- Total exacto de columnas nullable: 30.
- Columnas de esas 3 con regla completa confirmada: 1,
  PNA_FL_PERSONA.
- Columnas que continúan bloqueando el INSERT: 2,
  CNB_FL_CVE y CTO_FE_SOL_DESEMBOLSO.

El total físico exacto debe obtenerse mediante sys.columns antes de diseñar el
comando. Hasta entonces no se debe afirmar que el INSERT puede cubrir las 100
columnas ni implementar un mínimo.

## Decisiones confirmadas adicionales

### Numeración

- CPARAMETRO 19 con valor predeterminado 1 significa numeración por tipo de
  operación.
- CPARAMETRO 142 con descripción predeterminada 0 indica el uso de
  TOP_NO_CONSECUTIVO.
- CPARAMETRO 215 con valor predeterminado 1 indica consecutivo más operación.
- No existe una fila de KCONTRATO en CCATCONSEC; con la configuración actual
  esto no bloquea el alta.
- La aplicación debe llamar a spLsnetGeneraClaveContrato y nunca replicar su
  algoritmo en C#.

La llamada debe ocurrir dentro de la transacción Serializable. Se deben
validar NumError y StrLLaveContrato, además del límite de 15 caracteres.
Cualquier error debe revertir la reserva del consecutivo y el contrato.

### Operaciones

KTOPERACION es una vista sobre KTOPERACION_ORIGINAL. La vista excluye las
operaciones con TOP_FG_PP distinto de cero. El catálogo futuro debe filtrar
simultáneamente:

- TOP_FG_STATUS = 1;
- TOP_FG_PP = 0;
- EMP_FL_CVE igual a la empresa/tenant autorizado.

En Toyota se confirmaron 16 operaciones activas y no existen códigos
duplicados en ese conjunto.

### Fecha contable

CFECHA_OPERACION es la fecha contable autoritativa. El proceso nocturno la
actualiza. No se debe usar DateTime.Now para fechas Legacy derivadas. Si no
existe exactamente una fecha de operación válida, el caso de uso debe fallar
de forma controlada antes de reservar la clave o escribir el contrato.

### CNBV

El catálogo proviene de CCNB. La selección debe filtrar CNB_FG_STATUS = 1 y
la relación autorizada por TOP_CL_CVE. CNB_FG_REGDEFAULT sólo puede servir
como selección inicial de la UI; el backend debe volver a validar operación y
vigencia.

### CFDI

El catálogo autoritativo es dbo.CUSO_COMPROBANTES. Sólo son seleccionables
filas con UCO_FG_STATUS = 1. KCONTRATO guarda la clave UCO_CL_CLAVE; no se
debe usar UCO_FL_CVE.

La compatibilidad se resuelve con todos estos valores:

- CPERSONA.RFI_CL_CLAVE;
- KRELACION_REGIMEN_USOCFDI.RFI_CL_CLAVE;
- KRELACION_REGIMEN_USOCFDI.UCO_CL_CLAVE;
- KRELACION_REGIMEN_USOCFDI.RRU_FG_STATUS = 1;
- CUSO_COMPROBANTES.UCO_FG_STATUS = 1.

UCO_CL_PJURIDICA no se compara directamente con PNA_CL_PJURIDICA: el
catálogo observado usa 1 y 3, mientras CPERSONA usa 1, 2 y 20. La relación
régimen-uso es la evidencia funcional principal.

No existen claves activas duplicadas por clave y personalidad, ni relaciones
activas sin catálogo activo. Estas frecuencias son evidencia auxiliar, no una
regla para hardcodear.

El catálogo futuro debe devolver sólo usos compatibles con el cliente
seleccionado. El navegador enviará únicamente UCO_CL_CLAVE, nunca descripción
ni UCO_FL_CVE. El backend debe repetir toda la validación y responder
400 contract_cfdi_invalid si la clave es inexistente, inactiva o incompatible.
Sigue pendiente confirmar el conjunto completo de filas activas y las
relaciones huérfanas fuera de las comprobaciones recibidas.

## Resumen de cobertura de la matriz

La evidencia física autoritativa cubre 100 columnas: 70 NOT NULL y 30
nullable. La clasificación final de las 70 NOT NULL está en la matriz
exhaustiva del addendum; no se mantienen conteos parciales.

- Total de columnas NOT NULL: 70.
- Total de columnas nullable: 30.
- Columnas con origen Legacy confirmado: PNA_FL_PERSONA, CTO_FL_CVE,
  TOP_CL_CVE, CTO_FE_SOL_DESEMBOLSO y los campos explícitamente alimentados
  por controles de GuardaInfo.
- Columnas derivadas confirmadas: PNA_FL_PERSONA, CTO_FL_CVE mediante el
  procedimiento, CTO_FE_ULTMOD y USR_CL_CVE desde el contexto servidor,
  FlagIVAFronterizo y valores derivados por esquema.
- Columnas con constante confirmada: únicamente literales documentados por la
  ruta Legacy concreta, incluidos algunos 0, 1, NULL y GETDATE; no se
  generalizan a otras columnas.
- Columnas aún sin regla completa: todas las que carecen de origen,
  compatibilidad, default y nulabilidad confirmados, incluidos CNB_FL_CVE y
  CTO_FE_SOL_DESEMBOLSO como contrato moderno.

### Totales por categoría

No es posible proporcionar un total físico de NOT NULL ni totales completos
por categoría sin la nulabilidad de cada una de las 100 columnas. Con la
evidencia recibida sólo puede afirmarse:

- NOT NULL identificado: 3.
- NOT NULL con regla completa inicialmente confirmada: 1, PNA_FL_PERSONA;
  la matriz ampliada posterior agrega tres reglas derivadas y dos reglas
  calculadas confirmadas para la ruta nueva.
- NOT NULL sin regla completa: 2, CNB_FL_CVE y CTO_FE_SOL_DESEMBOLSO.
- Generada por procedimiento: CTO_FL_CVE.
- Derivadas: PNA_FL_PERSONA, CTO_FL_CVE, CTO_FE_ULTMOD, USR_CL_CVE,
  FlagIVAFronterizo y valores condicionados por esquema.
- Constantes confirmadas: sólo literales confirmados para la ruta Legacy
  concreta; no existe un conteo general seguro.
- Capturadas por usuario con regla completa: ninguna categoría financiera se
  considera completa para POST mientras falten catálogos, escalas y
  compatibilidades.

La lista exacta de columnas sin regla completa es:

CTO_NO_CONSECUTIVO, EMP_FL_CVE, SUC_FL_CVE, TAS_FL_CVE, KPR_FL_CVE,
KPQ_FL_CVE, LCR_FL_CVE, OPP_FL_CVE, CPB_FL_CVE, PRD_FL_CVE,
CTO_DS_CALIFICACION, CTO_FG_STATUS, CTO_CL_MONEDA, CTO_NO_CAPITAL,
CTO_NO_MTO_FINANCIAR, CTO_NO_MTO_ANTICIPO, CTO_NO_PRC_ANTICIPO,
CTO_NO_PLAZOORIGINAL, CTO_NO_MTO_ENGANCHE, CTO_NO_PRC_ENGANCHE,
CTO_NO_DEPRENTAS, CTO_NO_MTO_DEPRENTAS, CTO_CL_IVA, CTO_NO_MTO_DEPOSITO,
CNB_FL_CVE, CTO_CL_EDOEQUIPO, CTO_NO_TASA_BASE, CTO_NO_PUNTOS_ADIC,
CTO_NO_FACTOR, CTO_FE_PRIMER_PAGO, CTO_FE_ULTPAGO,
CTO_FE_SOL_DESEMBOLSO, CTO_FE_ACTIVACION, CTO_NO_TASA_NOMINAL,
CTO_CL_FPAGO_SEGBIEN, CTO_FE_BAJA, CTO_FE_FIRMA_CONTRATO,
CTO_FE_FIRMAANEXO, CTO_CL_EXIGIBILIDAD, CTO_CL_CALENDARIO, CTO_CL_FPAGO,
PPG_FL_CVE, CTO_FG_SEGVIDA, CTO_NO_MTO_VRESIDUAL, CTO_CL_ESQPAGO,
CTO_NO_PRC_VRESIDUAL, CTO_NO_VENACTUAL, CTO_NO_SALDO, CTO_CL_AMORT,
TLO_FL_CVE, CTO_DS_COMPOSICION, CTO_FG_TASA_REGULADA, CTO_NO_TASA_TECHO,
CTO_NO_TASA_PISO, TAS_FL_CVEMORA, TLO_FL_CVEMORA, TAS_NO_BASEMORA,
CTO_NO_PUNTOS_MORA, CTO_NO_FACTOR_MORA, CTO_NO_NOMINAL_MORA,
CTO_NO_MTO_OPCIONCOMPRA, CTO_NO_PRC_OPCIONCOMPRA, CTO_NO_TIR,
CTO_NO_PORC_CAT, CTO_NO_MTO_PAGOFINAL, CTO_NO_PRC_PAGOFINAL,
CTO_FG_CHKLIST, CTO_FG_MAESTRO, CTO_FL_CVE_MAESTRO, CTO_NO_ANEXO,
CTO_FE_ULTCALC_INTERES, CTO_NO_GRACIA_INT, CTO_FG_CESION, CTO_FG_CV,
SUC_FL_CVE, CTO_FG_OPC, SCB_FL_CVE, CTO_FG_TASA_USAORDINARIA,
CTO_NO_SDOANT, EQI_FL_CVE, CEC_CL_ESQPAGO, STC_FL_CVE, APP_FL_CVE,
DMO_FL_CVE, UCO_CL_CLAVE, CTO_FG_FRONTERIZO, CTO_FG_STATUS_FACTURACION,
CTO_FE_OPTERMINA, USR_CL_OPTERMINA, CTO_NO_EVM, CTO_NO_PORC_EVM, además de
las dos columnas físicas cuyos nombres no fueron entregados.

La lista expresa falta de regla completa, no afirma que todas sean NOT NULL.

### Reglas particulares por tipo de operación

La operación seleccionable debe pertenecer al tenant/empresa autorizado,
tener TOP_FG_STATUS = 1 y TOP_FG_PP = 0. Las operaciones marcadas como
propuesta no se incluyen en el alta directa. La combinación de operación,
esquema, producto, moneda, CNBV, CFDI, tasas y pagos finales debe validarse
en servidor; no se puede asumir que un mismo conjunto de campos aplica a
todas las operaciones.

En particular, los campos de anticipo, enganche, valor residual, pago final,
opción de compra, tasa regulada, tasa moratoria, seguro y condiciones de
amortización son condicionales al esquema/operación. La evidencia actual no
permite convertir esas condiciones en defaults universales.

## Conclusión de seguridad para POST

No es seguro implementar todavía POST /api/v1/contracts. La numeración,
operaciones, fecha contable, CNBV y compatibilidad CFDI ya tienen reglas
confirmadas, pero permanecen columnas con regla incompleta, dos columnas
físicas sin identificar, nulabilidad incompleta, efectos secundarios
condicionales y procedimientos cuyo rollback e idempotencia deben cerrarse.

### Tablas adicionales afectadas

En las rutas revisadas de ActualizaContrato se observaron, de forma
condicional:

- KCONTRATO: INSERT o actualización principal.
- KCONTRATO_CALCULO_TAMORTIZACION: actualización para una propuesta o
  inserción de la configuración de cálculo.
- tablas de características y cargos de producto, cuando la operación las
  activa, incluyendo KPRODUCTO_FACTURA y KCARAC_PROD_FACTURA.
- tablas de gracia o reestructura cuando los parámetros correspondientes
  están presentes.

La lista anterior es una traza de efectos observados, no una autorización
para escribirlos. Cada tabla adicional requiere contrato, columnas
obligatorias y frontera transaccional confirmados.

### Procedimientos y operaciones alrededor del INSERT

Antes del INSERT se observaron, según la ruta:

- obtención del esquema de financiamiento;
- SpDigitoVerificadorXPer, sólo cuando la configuración de referencia de pago
  lo requiere;
- COBM_SelBanamexR para la referencia bancaria;
- validaciones y consultas de producto/esquema.

Después del INSERT se observaron, según la ruta:

- InsertaTipoCalculoTablaAmortizacion;
- actualización o inserción de KCONTRATO_CALCULO_TAMORTIZACION;
- AsignaReestructura_Contrato si existe una reestructura;
- operaciones de características, facturación o cargos condicionadas por el
  producto.

No se debe ejecutar ninguno de estos efectos sin confirmar parámetros,
idempotencia, rollback y ausencia de PII en auditoría.

## Punto exacto de inserción Legacy

La sentencia de alta localizada está en:

- Archivo: E:Sitios IA CodexProleasenet_ToyotasdLsenetsd_clsContratoPropuesta.vb
- Clase: sd_clsContratoPropuesta
- Método: ActualizaContrato
- Representación: SQL dinámico construido en la variable strSQL
- Ejecución: _Tran.EjecutaQuery(strSQL)

La sentencia es un INSERT directo a KCONTRATO. No se encontró un
CommandText, SqlCommand o procedimiento almacenado que sustituya ese INSERT.
La generación de CTO_FL_CVE sí usa dbo.spLsnetGeneraClaveContrato y debe
ocurrir antes del INSERT dentro de la misma transacción. La cadena visible
termina en el SQL dinámico y en _Tran.EjecutaQuery; no se encontró un
ensamblado adicional que oculte el INSERT.

El INSERT enumera las columnas de KCONTRATO en grupos: clave/persona,
operación/empresa/plaza/tasas, producto y esquema, fechas, importes,
anticipo/enganche, CNBV, exigibilidad/calendario, pagos, amortización,
moratoria, pagos finales, auditoría, dirección, CFDI, facturación y EVM.
El orden de valores es posicional respecto de esa lista y procede de la
firma de ActualizaContrato y de literales del propio SQL. La lista completa de
columnas ya está transcrita en la matriz de 100 columnas de este documento;
no se reproduce aquí SQL propietario ni valores reales.

## Estado técnico frente a estado funcional

Para las 70 columnas NOT NULL se identificó una asignación técnica en la
firma, en el INSERT o en una constante de la ruta Legacy. Esto significa que
el valor que recibe la sentencia es trazable en el punto de escritura; no
significa que uCredit tenga aprobada la regla funcional para producirlo.

### Conteo técnico

- Origen técnico confirmado en el punto de INSERT: 70.
- Origen técnico no localizado en el punto de INSERT: 0.
- Origen upstream completamente confirmado para CD: 6:
  CTO_FL_CVE, PNA_FL_PERSONA, CTO_FE_GENERACION, CTO_FE_ULTMOD,
  CTO_NO_SALDO y CTO_FE_ULTPAGO.
- Valores técnicos provenientes de parámetros cuyo origen upstream para CD
  aún debe cerrarse: 64.

### Conteo funcional

- Reglas funcionales confirmadas: 6.
- Reglas funcionales parciales: 2, CNB_FL_CVE y
  CTO_FE_SOL_DESEMBOLSO.
- Reglas funcionales pendientes: 62.

La diferencia entre ambos conteos evita declarar resuelta una columna sólo
porque aparece en el INSERT. Las 64 columnas no resueltas técnicamente a nivel
de CD sí tienen una variable o parámetro visible, pero falta confirmar su
selección, default, catálogo, fórmula, aplicabilidad o valor exacto para CD.

## Cadena de parámetros para contrato nuevo

La secuencia observable es:

su_actContratoPropuesta.aspx.vb / GuardaInfo
→ variables de controles, ViewState, Session y valores precargados
→ sn_clsContratoPropuesta.ActualizaContrato
→ sd_clsContratoPropuesta.ActualizaContrato
→ cálculo de clave y valores derivados
→ INSERT dinámico de KCONTRATO.

Los parámetros técnicos identificados incluyen:

- strContrato: clave existente o vacío en alta; la clave nueva proviene del
  generador, no del navegador.
- intPersona: Request/CvePersona validado por el flujo Legacy; en uCredit
  debe provenir del cliente seleccionado del servidor.
- intPropuesta: cero para CD sin propuesta.
- intEmpresa: valor recibido desde la selección/contexto Legacy; para el MVP
  se solicita 1, pero la resolución moderna aún no está confirmada.
- strTipoOper: valor del selector de operación; para el MVP es CD.
- intLineaCred: línea seleccionada o automática; no se puede asumir NULL para
  CD sin confirmar LineaAutomatica.
- intPaquete, intOpcPaq, intProducto: controles de producto/paquete, con
  NULL cuando la ruta Legacy los considera ausentes.
- intPlaza: valor de la plaza seleccionada/preseleccionada.
- intStatCont: estado proporcionado por el flujo; valor inicial CD pendiente.
- intMoneda: catálogo seleccionado.
- dblPCapital y dblMtoFinanciar: capital y cálculo financiero.
- dblMtoAnt/dblPtjAnt: anticipo o enganche según flags del esquema.
- dblNoRent/dblMtoRent/dblDeposito: rentas y depósito según esquema.
- intCNB: selección CNBV compatible con operación.
- fechas: controles, CFECHA_OPERACION o cálculos posteriores, según la fecha.
- tasas ordinarias y moratorias: controles y catálogos de tasa.
- pagos finales, residual y opción de compra: controles condicionados por
  esquema.
- strUser y strFecOper: actor y fecha operativa del contexto.
- strCveUso/intDireccion: CFDI compatible y dirección activa del cliente.
- FlagIVAFronterizo: resultado de la consulta de persona.
- strEstatusFact, Monto_EVM y Porcentaje_EVM: controles/configuración aún
  pendientes para CD.

Los valores 0, 1, cadena vacía, NULL, fecha contable y fechas calculadas se
documentan como valores técnicos Legacy cuando aparecen en la ruta. La
decisión de conservar cada uno en uCredit sigue siendo funcional, salvo los
seis valores ya confirmados.

## Matriz CD: valor técnico esperado

Escenario: TOP_CL_CVE = CD, EMP_FL_CVE = 1, contrato nuevo, sin propuesta,
sin reestructura, sin contrato maestro, sin subsidio y sin seguro financiado.
Los valores son simbólicos y no son datos reales.

| Columna NOT NULL | Valor técnico CD | Origen en Legacy | Regla funcional |
|---|---|---|---|
| CTO_FL_CVE | <generatedContractKey> | spLsnetGeneraClaveContrato | Confirmada |
| EMP_FL_CVE | 1 | intEmpresa/contexto | Pendiente |
| TAS_FL_CVE | <selectedRateId> | selector de tasa | Pendiente |
| LCR_FL_CVE | <selectedCreditLineId> o NULL técnico | línea/LineaAutomatica | Pendiente |
| CTO_FE_GENERACION | <businessDate> | CFECHA_OPERACION | Confirmada |
| CTO_FE_INICIO | <capturedStartDate> | strFecIni | Pendiente |
| CTO_FG_REESTRUCTURA | 0 en el escenario | intFgReestructura | Parcial; aprobar constante |
| CTO_FG_STATUS | <initialContractStatus> | intStatCont | Pendiente |
| CTO_CL_MONEDA | <selectedCurrency> | catálogo moneda | Pendiente |
| CTO_NO_MTO_FINANCIAR | <calculatedFinancedAmount> | cálculo financiero | Pendiente |
| CTO_NO_MTO_ANTICIPO | <calculatedAdvanceAmount> o 0 | esquema | Pendiente |
| CTO_NO_PRC_ANTICIPO | <calculatedAdvancePercent> o 0 | esquema | Pendiente |
| CTO_NO_PLAZO | <term> | control plazo | Pendiente |
| CTO_NO_MTO_ENGANCHE | <calculatedDownPayment> o 0 | esquema | Pendiente |
| CTO_NO_PRC_ENGANCHE | <calculatedDownPaymentPercent> o 0 | esquema | Pendiente |
| CTO_NO_DEPRENTAS | <rentCount> o 0 | esquema | Pendiente |
| CTO_NO_MTO_DEPRENTAS | <rentAmount> o 0 | esquema | Pendiente |
| CTO_CL_IVA | <validatedTaxRateOrCode> | cliente/régimen | Pendiente |
| CTO_NO_MTO_DEPOSITO | <depositAmount> o 0 | esquema | Pendiente |
| CNB_FL_CVE | <selectedCnbId> | CCNB/TOP_CL_CVE | Parcial |
| CTO_CL_EDOEQUIPO | <equipmentStatus> | control de equipo | Pendiente |
| CTO_NO_TASA_BASE | <baseRate> | pestaña Tasa | Pendiente |
| CTO_NO_PUNTOS_ADIC | <additionalPoints> | pestaña Tasa | Pendiente |
| CTO_NO_FACTOR | <rateFactor> | pestaña Tasa | Pendiente |
| CTO_FE_PRIMER_PAGO | <capturedOrCalculatedFirstPaymentDate> | fecha/control | Pendiente |
| CTO_FE_ULTPAGO | <calculatedLastPaymentDate> | último CTP_FE_EXIGIBILIDAD | Confirmada |
| CTO_FE_SOL_DESEMBOLSO | <capturedOrDerivedDisbursementDate> | strFecSolDesem | Parcial |
| CTO_FE_ACTIVACION | <activationDateOrLegacyValue> | flujo de estado | Pendiente |
| CTO_NO_TASA_NOMINAL | <calculatedNominalRate> | cálculo de tasa | Pendiente |
| CTO_CL_FPAGO_SEGBIEN | <insurancePaymentMethod> | seguro | Pendiente |
| CTO_FE_BAJA | <legacyNullOrSentinelDate> | estado | Pendiente |
| CTO_FE_FIRMA_CONTRATO | <capturedContractSignatureDate> | formulario | Pendiente |
| CTO_FE_FIRMAANEXO | <capturedAnnexSignatureDate> | formulario | Pendiente |
| CTO_CL_EXIGIBILIDAD | <selectedDueRule> | catálogo | Pendiente |
| CTO_CL_CALENDARIO | <selectedCalendar> | catálogo | Pendiente |
| CTO_CL_FPAGO | <selectedPaymentMethod> | catálogo | Pendiente |
| PPG_FL_CVE | <selectedPaymentPeriodicity> | esquema pago | Pendiente |
| CTO_FG_SEGVIDA | 0 en el escenario sin seguro | intAplicaSegVida | Parcial; aprobar constante |
| CTO_NO_MTO_VRESIDUAL | <residualAmount> o 0 | esquema | Pendiente |
| CTO_CL_ESQPAGO | <selectedPaymentScheme> | esquema | Pendiente |
| CTO_NO_PRC_VRESIDUAL | <residualPercent> o 0 | esquema | Pendiente |
| CTO_NO_SALDO | <calculatedFinancedAmount> | dblSaldo = dblMtoFinanciar | Confirmada |
| CTO_CL_AMORT | <selectedAmortizationType> | catálogo | Pendiente |
| TLO_FL_CVE | <selectedCalculationType> | catálogo | Pendiente |
| CTO_FE_ULTMOD | <businessDate> | CFECHA_OPERACION | Confirmada |
| CTO_FG_TASA_REGULADA | <regulatedRateFlag> | pestaña Tasa | Pendiente |
| CTO_NO_TASA_TECHO | <rateCeiling> o 0 | pestaña Tasa | Pendiente |
| CTO_NO_TASA_PISO | <rateFloor> o 0 | pestaña Tasa | Pendiente |
| TAS_FL_CVEMORA | <moratoryRateId> | catálogo | Pendiente |
| TLO_FL_CVEMORA | <moratoryCalculationType> | catálogo | Pendiente |
| TAS_NO_BASEMORA | <moratoryBaseRate> | pestaña moratoria | Pendiente |
| CTO_NO_PUNTOS_MORA | <moratoryPoints> | pestaña moratoria | Pendiente |
| CTO_NO_FACTOR_MORA | <moratoryFactor> | pestaña moratoria | Pendiente |
| CTO_NO_NOMINAL_MORA | <calculatedMoratoryNominalRate> | cálculo | Pendiente |
| CTO_NO_MTO_OPCIONCOMPRA | <purchaseOptionAmount> o 0 | pagos finales | Pendiente |
| CTO_NO_PRC_OPCIONCOMPRA | <purchaseOptionPercent> o 0 | pagos finales | Pendiente |
| CTO_NO_MTO_PAGOFINAL | <finalPaymentAmount> o 0 | esquema | Pendiente |
| CTO_NO_PRC_PAGOFINAL | <finalPaymentPercent> o 0 | esquema | Pendiente |
| CTO_FG_CHKLIST | <legacyChecklistFlag> | flujo Legacy | Pendiente |
| CTO_FG_MAESTRO | 0 en el escenario | intEsContMtro | Parcial; aprobar constante |
| CTO_NO_ANEXO | <annexNumber> | flujo Legacy | Pendiente |
| CTO_FE_ULTCALC_INTERES | <interestCalculationDate> | cálculo | Pendiente |
| CTO_NO_GRACIA_INT | <interestGraceAmountOrRule> | esquema | Pendiente |
| CTO_FG_CESION | 0 en el escenario | intFgCesionado | Parcial; aprobar constante |
| CTO_FG_CV | <legacyCvFlag> | flujo Legacy | Pendiente |
| PNA_FL_PERSONA | <selectedPersonId> | cliente seleccionado | Confirmada |
| SCB_FL_CVE | <collectionStatusId> | cobranza/seguro | Pendiente |
| CTO_NO_SDOANT | 0 en contrato nuevo si la regla se confirma | flujo nuevo | Pendiente |
| CTO_NO_EVM | <evmAmount> o 0 | EVM/configuración | Pendiente |
| CTO_NO_PORC_EVM | <evmPercent> o 0 | EVM/configuración | Pendiente |

NULL sólo debe usarse si la columna y la ruta física lo permiten. Los
marcadores entre signos angulares no son valores aceptados por Legacy ni por
uCredit; representan el origen pendiente de resolver.

## Fechas de alta CD

| Columna | Origen técnico visible | Estado |
|---|---|---|
| CTO_FE_GENERACION | CFECHA_OPERACION | Confirmada |
| CTO_FE_INICIO | strFecIni/campo de inicio | Pendiente |
| CTO_FE_PRIMER_PAGO | strFecPrimerPago o ObtenFechas | Pendiente |
| CTO_FE_ULTPAGO | último CTP_FE_EXIGIBILIDAD | Confirmada |
| CTO_FE_SOL_DESEMBOLSO | strFecSolDesem, posible regla de primer pago | Pendiente |
| CTO_FE_ACTIVACION | strFecActivacion | Pendiente |
| CTO_FE_BAJA | strFecBaja o sentinel Legacy | Pendiente |
| CTO_FE_FIRMA_CONTRATO | strFecFirmaCont | Pendiente |
| CTO_FE_FIRMAANEXO | strFecFirmaAnexo | Pendiente |
| CTO_FE_ULTCALC_INTERES | cálculo posterior | Pendiente |
| CTO_FE_ULTMOD | CFECHA_OPERACION | Confirmada |

No se debe sustituir ninguna fecha pendiente por DateTime.Now. Las fechas
derivadas deben usar CFECHA_OPERACION o el cálculo Legacy equivalente.

## Bloqueos para un POST seguro

Bloquean actualmente un POST moderno:

1. nulabilidad individual y regla de todas las columnas físicas NOT NULL;
2. validación completa de CNB_FL_CVE por operación, vigencia y default;
3. origen moderno controlado de CTO_FE_SOL_DESEMBOLSO;
4. catálogo activo y relaciones válidas de CFDI;
5. contrato transaccional del procedimiento de generación de clave;
6. parámetros y rollback de tablas adicionales;
7. estado inicial y reglas de transición;
8. idempotencia y concurrencia de generación;
9. precisión física y fórmulas de tasas, importes y fechas;
10. actividad exacta de bitácora y límites de datos auditados.

Con estas decisiones queda confirmado el diseño de numeración y el catálogo
de operaciones, pero sigue prohibido implementar el INSERT hasta cerrar los
bloqueos anteriores.

## Matriz exhaustiva corregida de las 70 columnas NOT NULL

Los tipos siguientes son los tipos base del esquema autoritativo recibido; la
longitud, precisión y escala deben conservarse exactamente desde ese esquema
cuando se construyan parámetros tipados. La clasificación es conservadora:
capturar un valor en la pantalla no equivale a tener una regla de alta
confirmada.

| Columna | Tipo SQL | Fuente exacta | Clasificación | Evidencia Legacy | Validación | Aplicabilidad | Estado |
|---|---|---|---|---|---|---|---|
| CTO_FL_CVE | varchar(15) | spLsnetGeneraClaveContrato | generada por procedimiento | ActualizaContrato y SP confirmado | NumError, clave no vacía, máximo 15 | Todas | Confirmada |
| EMP_FL_CVE | int | contexto empresa/tenant | pendiente | GuardaInfo/ActualizaContrato reciben empresa | empresa autorizada y activa | Operación por empresa | Bloqueada |
| TAS_FL_CVE | int | selector de tasa | pendiente | Pestaña Tasa y parámetro intTasa | catálogo y vigencia | Operaciones con tasa | Bloqueada |
| LCR_FL_CVE | int | línea de crédito | pendiente | parámetro intLineaCred | pertenencia, vigencia y monto | Operaciones con línea | Bloqueada |
| CTO_FE_GENERACION | datetime | CFECHA_OPERACION | derivada | INSERT Legacy y fecha contable | una fecha operativa válida | Todas | Confirmada |
| CTO_FE_INICIO | datetime | formulario/fecha de inicio | pendiente | strFecIni en ActualizaContrato | fecha válida y regla operativa | Según operación | Bloqueada |
| CTO_FG_REESTRUCTURA | tinyint | flujo de reestructura | pendiente | intFgReestructura | sólo combinación autorizada | Reestructura | Bloqueada |
| CTO_FG_STATUS | tinyint | estado del flujo | pendiente | intStatCont | estado inicial autorizado | Todas | Bloqueada |
| CTO_CL_MONEDA | int | selector de moneda | pendiente | intMoneda | moneda activa y compatible | Todas | Bloqueada |
| CTO_NO_MTO_FINANCIAR | numeric | cálculo financiero | pendiente | dblMtoFinanciar | fórmula, escala y límites | Todas | Bloqueada |
| CTO_NO_MTO_ANTICIPO | numeric | esquema y condiciones | pendiente | dblMtoAnt condicionado por esquema | regla ESQ_FG_ANT_RENTA | Condicional | Bloqueada |
| CTO_NO_PRC_ANTICIPO | numeric | esquema y condiciones | pendiente | dblPtjAnt condicionado por esquema | porcentaje y escala | Condicional | Bloqueada |
| CTO_NO_PLAZO | int | control de plazo | pendiente | intPlazo y ValidaCampos | mayor que cero y catálogo | Operaciones financiadas | Bloqueada |
| CTO_NO_MTO_ENGANCHE | numeric | esquema y condiciones | pendiente | dblMtoAnt condicionado por esquema | regla ESQ_FG_ENGANCHE | Condicional | Bloqueada |
| CTO_NO_PRC_ENGANCHE | numeric | esquema y condiciones | pendiente | dblPtjAnt condicionado por esquema | porcentaje y escala | Condicional | Bloqueada |
| CTO_NO_DEPRENTAS | numeric | esquema de pago | pendiente | dblNoRent | escala y regla de rentas | Según esquema | Bloqueada |
| CTO_NO_MTO_DEPRENTAS | numeric | esquema de pago | pendiente | dblMtoRent | escala y regla de rentas | Según esquema | Bloqueada |
| CTO_CL_IVA | int/decimal según columna | régimen y cliente | pendiente | dblTasaIVA e IvaClienteFronterizo | catálogo y compatibilidad | Según operación | Bloqueada |
| CTO_NO_MTO_DEPOSITO | numeric | formulario/esquema | pendiente | dblDeposito | escala y regla del esquema | Según operación | Bloqueada |
| CNB_FL_CVE | int | CCNB y TOP_CL_CVE | pendiente | intCNB y su control | status 1, operación coincidente, default sólo inicial | Según operación | Bloqueada |
| CTO_CL_EDOEQUIPO | int | control de estado equipo | pendiente | intEdoEquipo | catálogo y vigencia | Operaciones con equipo | Bloqueada |
| CTO_NO_TASA_BASE | numeric | pestaña Tasa | pendiente | dblTasaBase | límites Legacy y escala | Según operación | Bloqueada |
| CTO_NO_PUNTOS_ADIC | numeric | pestaña Tasa | pendiente | dblPuntos | límites Legacy y escala | Según operación | Bloqueada |
| CTO_NO_FACTOR | numeric | pestaña Tasa | pendiente | dblFactor | límites Legacy y escala | Según operación | Bloqueada |
| CTO_FE_PRIMER_PAGO | datetime | formulario/cálculo de fecha | pendiente | strFecPrimerPago, ObtenFechas | fecha y periodicidad | Todas financiadas | Bloqueada |
| CTO_FE_ULTPAGO | datetime | cálculo de amortización | pendiente | INSERT/flujo de fechas | derivación por calendario | Todas financiadas | Bloqueada |
| CTO_FE_SOL_DESEMBOLSO | datetime | UI o CFECHA_OPERACION | pendiente | strFecSolDesem | origen aún no confirmado | Según operación | Bloqueada |
| CTO_FE_ACTIVACION | datetime | transición de estado | pendiente | strFecActivacion | estado y fecha contable | Activación | Bloqueada |
| CTO_NO_TASA_NOMINAL | numeric | cálculo de tasa | pendiente | dblTasaNom | fórmula reproducible y escala | Según operación | Bloqueada |
| CTO_CL_FPAGO_SEGBIEN | int | catálogo de seguro | pendiente | intFormaPagoSeg | catálogo/vigencia | Seguro aplicable | Bloqueada |
| CTO_FE_BAJA | datetime | transición de estado | pendiente | strFecBaja | sólo estados que permiten baja | Baja | Bloqueada |
| CTO_FE_FIRMA_CONTRATO | datetime | formulario | pendiente | strFecFirmaCont | fecha y estado | Según operación | Bloqueada |
| CTO_FE_FIRMAANEXO | datetime | formulario | pendiente | strFecFirmaAnexo | fecha y estado | Según operación | Bloqueada |
| CTO_CL_EXIGIBILIDAD | int | catálogo | pendiente | intExigibilidad | catálogo/vigencia | Según operación | Bloqueada |
| CTO_CL_CALENDARIO | int | catálogo | pendiente | intCalendario | catálogo/vigencia | Según operación | Bloqueada |
| CTO_CL_FPAGO | int | forma de pago | pendiente | intPeriodicidad/forma de pago | catálogo y compatibilidad | Según operación | Bloqueada |
| PPG_FL_CVE | int | esquema de pago | pendiente | intEsqPago | esquema válido | Según operación | Bloqueada |
| CTO_FG_SEGVIDA | tinyint | seguro | pendiente | intAplicaSegVida | 0/1 y regla del producto | Según operación | Bloqueada |
| CTO_NO_MTO_VRESIDUAL | numeric | esquema de residual | pendiente | dblMtoValRes | condición y escala | Condicional | Bloqueada |
| CTO_CL_ESQPAGO | int | esquema de pago | pendiente | intEsqPago | catálogo/vigencia | Todas financiadas | Bloqueada |
| CTO_NO_PRC_VRESIDUAL | numeric | esquema de residual | pendiente | dblPtjValRes | porcentaje y escala | Condicional | Bloqueada |
| CTO_NO_SALDO | numeric | cálculo inicial | pendiente | dblSaldo | fórmula y estado inicial | Todas | Bloqueada |
| CTO_CL_AMORT | int | catálogo amortización | pendiente | intCveAmor | catálogo y compatibilidad | Todas financiadas | Bloqueada |
| TLO_FL_CVE | int | tipo de cálculo | pendiente | intTipoCalc | catálogo y vigencia | Según operación | Bloqueada |
| CTO_FE_ULTMOD | datetime | CFECHA_OPERACION | derivada | INSERT y fecha contable | una fecha operativa válida | Todas | Confirmada |
| CTO_FG_TASA_REGULADA | tinyint | pestaña Tasa | pendiente | intAplicaTasaReg | 0/1 y campos condicionales | Condicional | Bloqueada |
| CTO_NO_TASA_TECHO | numeric | pestaña Tasa | pendiente | dblValTasaTecho | requerido si regulada | Condicional | Bloqueada |
| CTO_NO_TASA_PISO | numeric | pestaña Tasa | pendiente | dblValTasaPiso | requerido si regulada | Condicional | Bloqueada |
| TAS_FL_CVEMORA | int | catálogo tasa moratoria | pendiente | intTasaMora | catálogo/vigencia | Según operación | Bloqueada |
| TLO_FL_CVEMORA | int | catálogo cálculo moratorio | pendiente | intTipoCalcMora | catálogo/vigencia | Según operación | Bloqueada |
| TAS_NO_BASEMORA | numeric | pestaña moratoria | pendiente | dblTasaBaseMora | límites y escala | Según operación | Bloqueada |
| CTO_NO_PUNTOS_MORA | numeric | pestaña moratoria | pendiente | dblPuntosMora | límites y escala | Según operación | Bloqueada |
| CTO_NO_FACTOR_MORA | numeric | pestaña moratoria | pendiente | dblFactorMora | límites y escala | Según operación | Bloqueada |
| CTO_NO_NOMINAL_MORA | numeric | cálculo moratorio | pendiente | dblTasaNomMora | fórmula y escala | Según operación | Bloqueada |
| CTO_NO_MTO_OPCIONCOMPRA | numeric | pagos finales | pendiente | dblMtoOpcCompra | regla de opción y escala | Condicional | Bloqueada |
| CTO_NO_PRC_OPCIONCOMPRA | numeric | pagos finales | pendiente | dblPrjOpcCompra | regla de opción y escala | Condicional | Bloqueada |
| CTO_NO_MTO_PAGOFINAL | numeric | pagos finales | pendiente | dblMtoValRes | ESQ_FG_APLICA_PFINAL | Condicional | Bloqueada |
| CTO_NO_PRC_PAGOFINAL | numeric | pagos finales | pendiente | dblPtjValRes | ESQ_FG_APLICA_PFINAL | Condicional | Bloqueada |
| CTO_FG_CHKLIST | tinyint | flujo Legacy | pendiente | literal observado sin regla semántica | estado permitido | Según operación | Bloqueada |
| CTO_FG_MAESTRO | tinyint | flujo Legacy | pendiente | intEsContMtro | relación maestro válida | Condicional | Bloqueada |
| CTO_NO_ANEXO | int | flujo Legacy | pendiente | intAnexo | regla de anexos | Según operación | Bloqueada |
| CTO_FE_ULTCALC_INTERES | datetime | cálculo de intereses | pendiente | flujo de cálculo | fecha contable | Después de cálculo | Bloqueada |
| CTO_NO_GRACIA_INT | numeric | esquema de gracia | pendiente | valores del esquema | regla y escala | Condicional | Bloqueada |
| CTO_FG_CESION | tinyint | cesión | pendiente | intFgCesionado | 0/1 y regla | Condicional | Bloqueada |
| CTO_FG_CV | tinyint | flujo Legacy | pendiente | literal observado | significado pendiente | Según operación | Bloqueada |
| PNA_FL_PERSONA | int | cliente seleccionado | derivada | FK y llamada ActualizaContrato | CPERSONA existente | Todas | Confirmada |
| SCB_FL_CVE | int | seguro | pendiente | intAplicaSegVida | catálogo/vigencia | Condicional | Bloqueada |
| CTO_NO_SDOANT | numeric | saldo anterior | pendiente | valor de flujo | regla de reestructura | Condicional | Bloqueada |
| CTO_NO_EVM | numeric | EVM | pendiente | Monto_EVM | regla y escala | Condicional | Bloqueada |
| CTO_NO_PORC_EVM | numeric | EVM | pendiente | Porcentaje_EVM | rango y escala | Condicional | Bloqueada |

### Resumen cuantitativo

- Columnas NOT NULL totales: 70.
- Columnas confirmadas: 6.
- Capturadas por usuario con regla completa: 0.
- Derivadas confirmadas: 3, CTO_FE_GENERACION, CTO_FE_ULTMOD y
  PNA_FL_PERSONA.
- Generadas por procedimiento: 1, CTO_FL_CVE.
- Constantes Legacy confirmadas como regla general: 0.
- Columnas bloqueadas: 64.

La clasificación es deliberadamente estricta: CNB_FL_CVE tiene una regla
parcial, pero sigue bloqueada hasta cerrar catálogo, vigencia y compatibilidad;
CTO_FE_SOL_DESEMBOLSO sigue bloqueada porque no está confirmado si procede de
la UI, de CFECHA_OPERACION o del tipo de operación.

## Rastreo adicional del alta nueva

La ruta analizada es la de contrato nuevo sin propuesta, sin reestructura y
sin contrato maestro. Se excluyeron las ramas de modificación, propuesta,
reestructura y maestro.

### Cadena común confirmada

La cadena funcional observada es:

su_actContratoPropuesta.aspx
→ cmdGuardar_Click
→ GuardaInfo
→ ValidaCampos
→ sn_clsContratoPropuesta.ActualizaContrato
→ sd_clsContratoPropuesta.ActualizaContrato
→ INSERT/UPDATE de KCONTRATO
→ sn_clsContratoPropuesta.CalculaTPagos
→ ObtenPeriodosPagos
→ ActualizaFechaUltimoPago
→ CalculaCAT
→ cierre transaccional.

La llamada a ActualizaContrato recibe empresa, operación, línea, plaza,
moneda, producto, esquema, fechas, tasas, importes, CNBV, CFDI y dirección.
Que un parámetro exista no prueba por sí solo el valor de alta ni su regla.

Confirmaciones nuevas de cálculo:

- CTO_NO_SALDO se inicializa con el monto financiado en la ruta de inserción
  observada.
- CTO_FE_ULTPAGO se obtiene del último CTP_FE_EXIGIBILIDAD devuelto por
  ObtenPeriodosPagos y se aplica mediante ActualizaFechaUltimoPago.
- La tabla de pagos se calcula mediante CalculaTPagos después de la operación
  principal y antes del cierre del flujo.
- CalculaCAT se ejecuta antes de terminar la transacción cuando corresponde;
  si existe un CAT proporcionado, el flujo actualiza CTO_NO_PORC_CAT.
- Cuando conPropuesta es cero, se obtiene la persona y se generan cargos
  automáticos antes de finalizar el flujo.
- La excepción en ActualizaContrato cancela la transacción; el flujo externo
  también devuelve error y no debe permitir conservar una clave parcialmente
  creada.

### Matriz A: reglas comunes confirmadas

| Columna | Cadena de valor | Valor de alta confirmado | Aplicabilidad | Estado |
|---|---|---|---|---|
| CTO_FL_CVE | GuardaInfo → ActualizaContrato → spLsnetGeneraClaveContrato | StrLLaveContrato, máximo 15 | Todas | Confirmada |
| PNA_FL_PERSONA | Cliente seleccionado → intPersona → ActualizaContrato | PersonId validado | Todas | Confirmada |
| CTO_FE_GENERACION | Fecha Legacy → fecha de operación → INSERT | CFECHA_OPERACION | Todas | Confirmada |
| CTO_FE_ULTMOD | Fecha Legacy → fecha de operación → INSERT/UPDATE | CFECHA_OPERACION | Todas | Confirmada |
| CTO_NO_SALDO | monto financiado → dblSaldo → INSERT | dblMtoFinanciar en alta nueva | Todas | Confirmada para la ruta |
| CTO_FE_ULTPAGO | CalculaTPagos → ObtenPeriodosPagos → último CTP_FE_EXIGIBILIDAD | Último vencimiento calculado | Todas con pagos | Confirmada para la ruta |

### Matriz B: reglas específicas o condicionales

| Grupo | Condición | Evidencia | Estado para CD |
|---|---|---|---|
| Anticipo | ESQ_FG_ANT_RENTA | ActualizaContrato decide entre monto/porcentaje o cero | Pendiente de esquema CD |
| Enganche | ESQ_FG_ENGANCHE | ActualizaContrato decide entre monto/porcentaje o cero | Pendiente de esquema CD |
| Residual | ESQ_FG_APLICA_VRESIDUAL | Valor residual y porcentaje se insertan sólo si aplica | Pendiente de esquema CD |
| Pago final | ESQ_FG_APLICA_PFINAL | Monto y porcentaje finales se insertan sólo si aplica | Pendiente de esquema CD |
| Tasa regulada | intAplicaTasaReg | Piso/techo dependen de la marca regulada | Pendiente de operación CD |
| Tasa moratoria | tasa, tipo de cálculo y parámetros moratorios | ObtieneTasaDefault/ActualizaContrato | Pendiente de catálogo CD |
| Seguro | intFormaPagoSeg/intAplicaSegVida | Controles y parámetros de seguro | Excluido del MVP solicitado, pero NOT NULL requiere regla |
| Subsidio | parámetros posteriores a ActualizaContrato | Flujo de esquema de pago adicional | Excluido del MVP; no asumir cero sin regla física |
| CFDI | CUSO_COMPROBANTES y relación de régimen | Validación de uso compatible | Pendiente de filas activas confirmadas |
| CNBV | CCNB y TOP_CL_CVE | status, operación y default inicial | Regla parcial; pendiente para CD |

### Matriz C: reglas no confirmadas

Las siguientes columnas continúan sin cadena completa de valor y validación
para una operación CD: EMP_FL_CVE, TAS_FL_CVE, LCR_FL_CVE,
CTO_FE_INICIO, CTO_FG_REESTRUCTURA, CTO_FG_STATUS, CTO_CL_MONEDA,
CTO_NO_MTO_FINANCIAR, CTO_NO_MTO_ANTICIPO, CTO_NO_PRC_ANTICIPO,
CTO_NO_PLAZO, CTO_NO_MTO_ENGANCHE, CTO_NO_PRC_ENGANCHE,
CTO_NO_DEPRENTAS, CTO_NO_MTO_DEPRENTAS, CTO_CL_IVA, CTO_NO_MTO_DEPOSITO,
CNB_FL_CVE, CTO_CL_EDOEQUIPO, CTO_NO_TASA_BASE, CTO_NO_PUNTOS_ADIC,
CTO_NO_FACTOR, CTO_FE_PRIMER_PAGO, CTO_FE_SOL_DESEMBOLSO,
CTO_FE_ACTIVACION, CTO_NO_TASA_NOMINAL, CTO_CL_FPAGO_SEGBIEN,
CTO_FE_BAJA, CTO_FE_FIRMA_CONTRATO, CTO_FE_FIRMAANEXO,
CTO_CL_EXIGIBILIDAD, CTO_CL_CALENDARIO, CTO_CL_FPAGO, PPG_FL_CVE,
CTO_FG_SEGVIDA, CTO_NO_MTO_VRESIDUAL, CTO_CL_ESQPAGO,
CTO_NO_PRC_VRESIDUAL, CTO_CL_AMORT, TLO_FL_CVE,
CTO_FG_TASA_REGULADA, CTO_NO_TASA_TECHO, CTO_NO_TASA_PISO,
TAS_FL_CVEMORA, TLO_FL_CVEMORA, TAS_NO_BASEMORA, CTO_NO_PUNTOS_MORA,
CTO_NO_FACTOR_MORA, CTO_NO_NOMINAL_MORA, CTO_NO_MTO_OPCIONCOMPRA,
CTO_NO_PRC_OPCIONCOMPRA, CTO_NO_MTO_PAGOFINAL, CTO_NO_PRC_PAGOFINAL,
CTO_FG_CHKLIST, CTO_FG_MAESTRO, CTO_NO_ANEXO, CTO_FE_ULTCALC_INTERES,
CTO_NO_GRACIA_INT, CTO_FG_CESION, CTO_FG_CV, SCB_FL_CVE, CTO_NO_SDOANT,
CTO_NO_EVM y CTO_NO_PORC_EVM.

En esta lista, CTO_NO_SALDO, CTO_FE_GENERACION, CTO_FE_ULTMOD,
CTO_FE_ULTPAGO, CTO_FL_CVE y PNA_FL_PERSONA ya fueron retiradas por tener
reglas confirmadas para la ruta analizada. La lista no afirma que todas sean
NOT NULL adicionales: sólo enumera columnas NOT NULL cuyo valor de alta CD
carece todavía de una regla completa.

## Evaluación MVP: CD, empresa 1, contrato nuevo

Supuestos de análisis: TOP_CL_CVE = CD, EMP_FL_CVE = 1, sin propuesta, sin
reestructura, sin contrato maestro, sin subsidio y sin seguros financiados.

La evaluación todavía no queda completa. Los bloqueos principales son:

- no está documentado cómo se resuelven empresa 1, línea de crédito, plaza,
  tasa, moneda, calendario, exigibilidad y forma de pago para CD;
- no está confirmado el estado inicial de contrato;
- no está resuelto el origen de CTO_FE_SOL_DESEMBOLSO;
- no están cerrados capital, monto financiado, anticipo, enganche, tasas y CAT;
- no están cerrados CNBV, CFDI, amortización y pagos finales;
- no está confirmado qué columnas NOT NULL reciben constantes cero o uno en
  esta operación y cuáles reciben un valor del esquema;
- no se ha cerrado el contrato de los cargos automáticos, facturación y
  cálculo de amortización.

## Procedimientos y tablas involucrados en CD

Procedimientos y métodos observados antes o durante el INSERT:

- ObtenEsquemaFinanciamiento;
- spLsnetGeneraClaveContrato;
- SpDigitoVerificadorXPer, sólo si aplica la configuración de referencia;
- COBM_SelBanamexR;
- ActualizaContrato;
- cálculos y consultas de producto/esquema.

Después del INSERT o antes del cierre del flujo:

- CalculaTPagos;
- ObtenPeriodosPagos;
- ActualizaFechaUltimoPago;
- CalculaCAT;
- ActualizaContratoEsquemaPagoAdicional;
- GenerarCargosAutomaticos;
- InsertaTipoCalculoTablaAmortizacion o actualización de
  KCONTRATO_CALCULO_TAMORTIZACION;
- GrabaContratoCargoInicial cuando existen cargos;
- operaciones de reestructura sólo fuera del MVP.

Tablas afectadas u observadas en estas rutas:

- KCONTRATO;
- KCONTRATO_CALCULO_TAMORTIZACION;
- KTPAGO_CONTRATO mediante el cálculo de pagos;
- tablas de cargos/facturación como KCTO_FACT, KPRODUCTO_FACTURA y
  KCARAC_PROD_FACT, cuando el producto las activa;
- tablas de cargos iniciales cuando existen cargos.

La frontera exacta de cada procedimiento y su rollback debe verificarse antes
de trasladar el flujo a Dapper. La transacción debe abarcar generación de
clave, INSERT, cálculos, tablas relacionadas y auditoría.

## Resultado de esta etapa

- Columnas NOT NULL con regla confirmada para cualquier operación: 6,
  incluyendo clave, persona, fechas de generación/última modificación,
  saldo inicial y último pago calculado.
- Columnas NOT NULL aún bloqueadas para cualquier operación: 64.
- Columnas NOT NULL aún bloqueadas específicamente para CD: 64.
- El MVP CD no tiene una matriz completa.
- No es seguro implementar todavía un primer POST limitado a CD.
