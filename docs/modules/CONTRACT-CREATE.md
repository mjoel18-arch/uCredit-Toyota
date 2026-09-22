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
| intLineaCred | LCR_FL_CVE | Línea de crédito | Resultado interno de generación automática | No se acepta desde el navegador; requiere rama Legacy de automatización | Parcial | `sn_clsCredito.LineaAutomatica` y creación en `sn_clsContratos` |
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
- intLineaCred: no es una selección del usuario. Para el MVP CD, `LCR_FL_CVE`
  debe ser un resultado interno de generación automática; el payload moderno
  no lo acepta y no puede enviar un identificador arbitrario.
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
| LCR_FL_CVE | <generatedCreditLineId> | generación automática Legacy | Parcial; falta cerrar esquema y rama CD |
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

## Enlace upstream de las 70 columnas

En el Legacy, GuardaInfo arma la llamada posicional a
sn_clsContratoPropuesta.ActualizaContrato. El adaptador de negocio reenvía
los argumentos a sd_clsContratoPropuesta.ActualizaContrato. El último punto
de la cadena es el SQL dinámico strSQL. Por ello, una columna puede tener
origen técnico localizado aunque su regla funcional moderna continúe pendiente.

Abreviaturas usadas en la matriz:

- GF: GuardaInfo / controles de su_actContratoPropuesta.aspx.
- ACP: sn_clsContratoPropuesta.ActualizaContrato.
- DCP: sd_clsContratoPropuesta.ActualizaContrato.
- OP: CFECHA_OPERACION.
- ESQ: ObtenEsquemaFinanciamiento.
- CALC: cálculo de pagos, tasa o importes.
- CAT: catálogo Legacy.

| Columna | Expresión/valor en INSERT | Parámetro o argumento | Origen upstream | Valor CD | Evidencia | Regla |
|---|---|---|---|---|---|---|
| CTO_FL_CVE | strLlaveContrato | strContrato/strNoOper | ACP/DCP/SP | <generatedContractKey> | GuardaInfo, ACP, DCP | Confirmada |
| EMP_FL_CVE | intEmpresa | intEmpresa | GF/contexto | 1 | GuardaInfo y firma ACP | Pendiente aprobación |
| TAS_FL_CVE | intTasa | intTasa | cmbTipoTasa | <selectedRateId> | ASPX, GuardaInfo | CAT pendiente |
| LCR_FL_CVE | resultado de generación automática | `LineaAutomatica` + alta de `KLINEA_CREDITO` | servidor Legacy/servicio de contrato | <generatedCreditLineId> | `sn_clsContratos`, `sn_clsCredito`, `sd_clsCredito` | Parcial; no es entrada del usuario |
| CTO_FE_GENERACION | CFECHA_OPERACION | strFecOper | OP | <businessDate> | DCP INSERT | Confirmada |
| CTO_FE_INICIO | strFecIni | strFecIni | control fecha inicio | <startDate> | GF/ValidaCampos | Fecha pendiente |
| CTO_FG_REESTRUCTURA | intFgReestructura | intFgReestructura | flujo reestructura | 0 en MVP | GF/ACP | Constante por aprobar |
| CTO_FG_STATUS | intStatCont | intStatCont | estado/carga | <initialStatus> | GF/ACP | Estado pendiente |
| CTO_CL_MONEDA | intMoneda | intMoneda | cmbMoneda | <nationalCurrency> | GF/ACP | CAT pendiente |
| CTO_NO_MTO_FINANCIAR | dblMtoFinanciar | ByRef dblMtoFinanciar | cálculo financiero | <calculatedFinancedAmount> | ACP y DCP UPDATE/INSERT | Cálculo pendiente |
| CTO_NO_MTO_ANTICIPO | IIf(ESQ_FG_ANT_RENTA=1,dblMtoAnt,0) | dblMtoAnt | ESQ + GF | 0 o <advanceAmount> | DCP INSERT | Esquema pendiente |
| CTO_NO_PRC_ANTICIPO | IIf(ESQ_FG_ANT_RENTA=1,dblPtjAnt,0) | dblPtjAnt | ESQ + GF | 0 o <advancePercent> | DCP INSERT | Esquema pendiente |
| CTO_NO_PLAZO | intPlazo | intPlazo | txtPlazo | <term> | ValidaCampos | Regla pendiente |
| CTO_NO_MTO_ENGANCHE | IIf(ESQ_FG_ENGANCHE=1,dblMtoAnt,0) | dblMtoAnt | ESQ + GF | 0 o <downPayment> | DCP INSERT | Esquema pendiente |
| CTO_NO_PRC_ENGANCHE | IIf(ESQ_FG_ENGANCHE=1,dblPtjAnt,0) | dblPtjAnt | ESQ + GF | 0 o <downPaymentPercent> | DCP INSERT | Esquema pendiente |
| CTO_NO_DEPRENTAS | dblNoRent | dblNoRent | txtRentas | 0 o <rentCount> | GF/ValidaCampos | Esquema pendiente |
| CTO_NO_MTO_DEPRENTAS | dblMtoRent | dblMtoRent | txtMonto | 0 o <rentAmount> | GF | Esquema pendiente |
| CTO_CL_IVA | dblTasaIVA | dblTasaIVA | cliente/régimen | <validatedTaxValue> | IvaClienteFronterizo | Compatibilidad pendiente |
| CTO_NO_MTO_DEPOSITO | dblDeposito | dblDeposito | txtDeposito/esquema | 0 o <depositAmount> | GF/ACP | Esquema pendiente |
| CNB_FL_CVE | intCNB | intCNB | ddlCNB/su_catCNB | <selectedCnbId> | GF/CCNB | Compatibilidad parcial |
| CTO_CL_EDOEQUIPO | intEdoEquipo | intEdoEquipo | control equipo | <equipmentStatus> | GF | CAT pendiente |
| CTO_NO_TASA_BASE | dblTasaBase | dblTasaBase | txtTasaBase | <baseRate> | pestaña Tasa | Cálculo pendiente |
| CTO_NO_PUNTOS_ADIC | dblPuntos | dblPuntos | txtPuntos | <additionalPoints> | pestaña Tasa | Cálculo pendiente |
| CTO_NO_FACTOR | dblFactor | dblFactor | txtFactor | <rateFactor> | pestaña Tasa | Cálculo pendiente |
| CTO_FE_PRIMER_PAGO | strFecPrimerPago | strFecPrimerPago | control/ObtenFechas | <firstPaymentDate> | GF/ObtenFechas | Fecha pendiente |
| CTO_FE_ULTPAGO | Format(last CTP_FE_EXIGIBILIDAD) | recalculado DCP | ObtenPeriodosPagos | <calculatedLastPaymentDate> | ACP | Confirmada |
| CTO_FE_SOL_DESEMBOLSO | strFecSolDesem | strFecSolDesem | control/regla | <disbursementDate> | GF/ValidaCampos | Origen pendiente |
| CTO_FE_ACTIVACION | strFecActivacion | strFecActivacion | estado | <activationDate> | GF/DCP | Fecha pendiente |
| CTO_NO_TASA_NOMINAL | dblTasaNom | dblTasaNom | cálculo tasa | <nominalRate> | pestaña Tasa | Fórmula pendiente |
| CTO_CL_FPAGO_SEGBIEN | intFormaPagoSeg | intFormaPagoSeg | seguro | <insurancePaymentMethod> | GF | CAT pendiente |
| CTO_FE_BAJA | strFecBaja | strFecBaja | estado | <legacySentinelDate> | GF | Sentinel pendiente |
| CTO_FE_FIRMA_CONTRATO | strFecFirmaCont | strFecFirmaCont | control firma | <contractSignatureDate> | GF | Fecha pendiente |
| CTO_FE_FIRMAANEXO | strFecFirmaAnexo | strFecFirmaAnexo | control firma | <annexSignatureDate> | GF | Fecha pendiente |
| CTO_CL_EXIGIBILIDAD | intExigibilidad | intExigibilidad | catálogo | <dueRule> | GF | CAT pendiente |
| CTO_CL_CALENDARIO | intCalendario | intCalendario | catálogo | <calendar> | GF | CAT pendiente |
| CTO_CL_FPAGO | 1 en INSERT Legacy | intPeriodicidad | esquema | <paymentMethod> | DCP INSERT | Constante/significado pendiente |
| PPG_FL_CVE | intPeriodicidad | intPeriodicidad | cmbPeriodicidad | <paymentPeriodicity> | GF | CAT pendiente |
| CTO_FG_SEGVIDA | intAplicaSegVida | intAplicaSegVida | seguro | 0 en MVP | GF/ACP | Constante por aprobar |
| CTO_NO_MTO_VRESIDUAL | IIf(flag residual,dblMtoValRes,0) | dblMtoValRes | esquema | 0 en MVP | DCP INSERT | Regla pendiente |
| CTO_CL_ESQPAGO | intEsqPago | intEsqPago | esquema | <paymentScheme> | GF | CAT pendiente |
| CTO_NO_PRC_VRESIDUAL | IIf(flag residual,dblPtjValRes,0) | dblPtjValRes | esquema | 0 en MVP | DCP INSERT | Regla pendiente |
| CTO_NO_SALDO | dblSaldo=dblMtoFinanciar | dblSaldo | ACP cálculo | <calculatedFinancedAmount> | ACP | Confirmada |
| CTO_CL_AMORT | intCveAmor | intCveAmor | catálogo | <amortizationType> | GF | CAT pendiente |
| TLO_FL_CVE | intTipoCalc | intTipoCalc | catálogo tasa | <calculationType> | GF | CAT pendiente |
| CTO_FE_ULTMOD | CFECHA_OPERACION | strFecOper | OP | <businessDate> | DCP INSERT | Confirmada |
| CTO_FG_TASA_REGULADA | intAplicaTasaReg | intAplicaTasaReg | checkbox tasa | <regulatedFlag> | GF | Condicional |
| CTO_NO_TASA_TECHO | IIf(regulada,dblValTasaTecho,0) | dblValTasaTecho | tasa | 0 o <rateCeiling> | DCP INSERT | Condicional |
| CTO_NO_TASA_PISO | IIf(regulada,dblValTasaPiso,0) | dblValTasaPiso | tasa | 0 o <rateFloor> | DCP INSERT | Condicional |
| TAS_FL_CVEMORA | intTasaMora | intTasaMora | ObtieneTasaDefault | <moratoryRateId> | GF/ObtieneTasaDefault | CAT pendiente |
| TLO_FL_CVEMORA | intTipoCalcMora | intTipoCalcMora | ObtieneTasaDefault | <moratoryCalcType> | GF | CAT pendiente |
| TAS_NO_BASEMORA | dblTasaBaseMora | dblTasaBaseMora | tasa moratoria | <moratoryBase> | pestaña moratoria | Cálculo pendiente |
| CTO_NO_PUNTOS_MORA | dblPuntosMora | dblPuntosMora | tasa moratoria | <moratoryPoints> | pestaña moratoria | Cálculo pendiente |
| CTO_NO_FACTOR_MORA | dblFactorMora | dblFactorMora | tasa moratoria | <moratoryFactor> | pestaña moratoria | Cálculo pendiente |
| CTO_NO_NOMINAL_MORA | dblTasaNomMora | dblTasaNomMora | cálculo moratorio | <moratoryNominal> | pestaña moratoria | Fórmula pendiente |
| CTO_NO_MTO_OPCIONCOMPRA | dblMtoOpcCompra | dblMtoOpcCompra | pagos finales | 0 en MVP | DCP INSERT | Constante por aprobar |
| CTO_NO_PRC_OPCIONCOMPRA | dblPrjOpcCompra | dblPrjOpcCompra | pagos finales | 0 en MVP | DCP INSERT | Constante por aprobar |
| CTO_NO_MTO_PAGOFINAL | IIf(flag final,dblMtoValRes,0) | dblMtoValRes | esquema | 0 en MVP | DCP INSERT | Constante por aprobar |
| CTO_NO_PRC_PAGOFINAL | IIf(flag final,dblPtjValRes,0) | dblPtjValRes | esquema | 0 en MVP | DCP INSERT | Constante por aprobar |
| CTO_FG_CHKLIST | literal 0 observado | no expuesto | constante | SQL DCP | 0 técnico | Significado pendiente |
| CTO_FG_MAESTRO | intEsContMtro | intEsContMtro | flujo maestro | 0 en MVP | GF/ACP | Constante por aprobar |
| CTO_NO_ANEXO | intAnexo | intAnexo | flujo Legacy | <annexNumber> | GF | Regla pendiente |
| CTO_FE_ULTCALC_INTERES | fecha del cálculo | cálculo | procedimiento | <interestCalculationDate> | DCP posterior | Regla pendiente |
| CTO_NO_GRACIA_INT | valor de esquema | ESQ | catálogo/cálculo | <graceInterest> | DCP/ESQ | Cálculo pendiente |
| CTO_FG_CESION | intFgCesionado | flujo cesión | constante para MVP | 0 en MVP | GF/ACP | Constante por aprobar |
| CTO_FG_CV | literal de ruta | SQL DCP | constante | DCP INSERT | <legacyCvFlag> | Significado pendiente |
| PNA_FL_PERSONA | intPersona | cliente seleccionado | derivada | <selectedPersonId> | GF/ACP/FK | Confirmada |
| SCB_FL_CVE | intAplicaSegVida/estado cobranza | seguro/cobranza | procedimiento/catálogo | <collectionStatusId> | DCP | Origen pendiente |
| CTO_NO_SDOANT | literal o reestructura | flujo especial | constante/condicional | 0 en MVP | DCP/reestructura | Constante por aprobar |
| CTO_NO_EVM | Monto_EVM | control/configuración | parámetro | 0 o <evmAmount> | GF | Regla pendiente |
| CTO_NO_PORC_EVM | Porcentaje_EVM | control/configuración | parámetro | 0 o <evmPercent> | GF | Regla pendiente |

La tabla enlaza cada columna con el punto técnico visible. Los valores
marcados como cero son valores técnicos de la ruta Legacy o del escenario
MVP; aún requieren aprobación explícita para conservarlos en uCredit.

## Causas de las 64 reglas funcionales pendientes

La clasificación no trata el origen técnico como desconocido:

| Causa | Total |
|---|---:|
| Fuente Legacy no encontrada | 0 |
| Archivo o ensamblado faltante | 0 |
| Valor visible pero significado funcional pendiente | 12 |
| Constante visible pendiente de aprobación | 6 |
| Cálculo visible pendiente de validar | 18 |
| Catálogo identificado sin filtro/compatibilidad cerrada | 12 |
| Dependencia del tipo de operación | 6 |
| Dependencia de amortización | 4 |
| Dependencia de línea de crédito | 1 |
| No aplica al MVP CD pero KCONTRATO exige valor técnico | 5 |
| **Total** | **64** |

## Campos adicionales que Legacy necesita para CD

| Campo adicional | Motivo | ¿Derivable? | ¿Nuevo control? | Catálogo | Decisión |
|---|---|---|---|---|---|
| Empresa/plaza | Determina alcance y sucursal operativa | Parcialmente | Sí o resolución server-side | CEMPRESA/plaza | Aprobar resolución por despliegue |
| Línea de crédito | Alimenta LCR_FL_CVE y límites | Sí, mediante resolución automática | No; sólo resultado interno | KLINEA_CREDITO/KLTOPERA | Aprobado: el usuario no selecciona la línea; falta cerrar esquema y valores de la rama CD |
| Periodicidad/esquema | Calcula pagos y último vencimiento | No | Sí | CPERPAGO/esquema | Aprobar conjunto CD |
| Tipo y cálculo de tasa | Alimenta tasas ordinaria/moratoria | Parcial | Sí | CTASA/tipos | Aprobar tasas fijas CD |
| CNBV | Columna NOT NULL y compatibilidad operativa | No | Sí | CCNB | Completar regla CD |
| Uso CFDI | Validación fiscal y clave almacenada | No | Sí | CUSO_COMPROBANTES/relación | Confirmar activo compatible |
| Estado de equipo | Columna obligatoria | No confirmada | Sí si aplica | Catálogo equipo | Decidir valor CD |
| Seguro | Columnas obligatorias relacionadas | No confirmada | No si MVP lo excluye, pero requiere valor | Catálogo seguro | Aprobar constantes de no seguro |
| Fechas de firma/desembolso | Cumplimiento del flujo | Algunas | Sí | CFECHA_OPERACION/controles | Confirmar fechas CD |
| Pagos finales/residual | Evita valores incompletos | Por esquema | Sí | Esquema amortización | Aprobar “sin pagos especiales” |

## Separación final de estados

### A. Origen upstream

- Confirmado: 6 columnas.
- Localizado técnicamente pero upstream CD incompleto: 64.
- Fuente o ensamblado faltante: 0 en las rutas revisadas.

### B. Comportamiento Legacy

- Confirmado: asignación SQL, generación de clave, fecha contable, saldo
  inicial, último pago y cancelación de transacción.
- Parcialmente confirmado: estado inicial, CNBV, desembolso, tasas, CFDI,
  línea, seguro y campos condicionales.
- Desconocido: significado funcional de algunos literales y defaults de
  campos no expuestos.

### C. Decisiones modernas

- Puede conservarse exactamente: uso de CFECHA_OPERACION, generación de clave
  por SP, rollback y derivación de saldo/último pago.
- Requiere aprobación: constantes cero/uno, estado inicial, fechas centinela,
  “sin seguro”, “sin pagos especiales” y valores EVM.
- Requiere campo adicional o resolución server-side: empresa, plaza, línea,
  esquema, tasas, CNBV, CFDI y fechas.
- Fuera del MVP CD: propuesta, reestructura, contrato maestro, subsidio,
  seguros financiados y efectos de propuesta.

## Decisiones concretas pendientes de aprobación

1. Valor y resolución server-side de empresa/plaza para CD.
2. Cerrar la rama exacta de generación automática para CD: esquema físico de `KLINEA_CREDITO`, valores de cada NOT NULL, compatibilidad empresa/operación/moneda/tasa y semántica de asociación.
3. Moneda nacional, esquema, periodicidad, tasas ordinarias y moratorias.
4. Estado inicial y fechas de desembolso, activación, firma y baja.
5. Valores “sin seguro”, “sin residual”, “sin opción” y “sin pagos finales”.
6. Valor y compatibilidad CNBV, CFDI y estado de equipo.
7. Inclusión y alcance de cargos automáticos, facturación y CAT en la primera
   transacción moderna.

Conclusión: el MVP CD está bloqueado por 7 decisiones concretas, no por una
fuente Legacy ausente. Podrá considerarse listo sólo después de aprobarlas y
confirmar los catálogos y cálculos correspondientes.

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

## Línea de crédito automática aprobada para el MVP CD

### Decisión funcional

La línea de crédito no será seleccionada por el usuario y `LCR_FL_CVE` no
forma parte del request HTTP. Para CD, el backend debe generar o resolver la
línea dentro del flujo de alta y utilizar el identificador resultante al
insertar `KCONTRATO`. Un identificador enviado por el navegador, un `NULL`
arbitrario o un valor inventado deben rechazarse. Si la creación de la línea
falla, el contrato no puede confirmarse.

### Evidencia Legacy localizada

| Archivo y método | Evidencia funcional | Orden observado |
|---|---|---|
| `Proleasenet.Negocio/sn_clsCredito.vb`, `LineaAutomatica` | Consulta la empresa y habilita la automatización según `EMP_FG_AUT_PROPUESTA` para opción 1 o `EMP_FG_AUT_CONTRATO` para opción 2. | Validación previa de la modalidad; no genera por sí sola una fila. |
| `sdLsenet/sd_clsContratoPropuesta.vb`, `LineaAutomatica` | Repite la decisión leyendo `CEMPRESA` y devuelve si la empresa permite línea automática para contrato. | Se invoca desde el flujo de captura antes de la persistencia del contrato. |
| `Proleasenet.Negocio/sn_clsContratos.vb`, rama `If blnLinAut Then` | Reserva `KLINEA_CREDITO`, construye `Linea_credito` con persona, moneda, monto financiado, plazo, tasas/valores iniciales y usuario, y llama `sn_clsCredito.ActualizaLineaCredito`. | 1) consecutivo; 2) alta de la línea; 3) recarga de la línea; 4) `ActualizaOperacionesLinea`; 5) continúa el flujo del contrato. |
| `sdLsenet/sd_clsCredito.vb`, `ActualizaLineaUsuario` | En el alta obtiene el consecutivo mediante `ObtenConsecutivo("KLINEA_CREDITO", ...)`, inserta la línea y sincroniza `KLTOPERA` para las operaciones recibidas; registra bitácora cuando la bandera lo solicita. | La rutina tiene transacción propia si no recibe una transacción externa. |
| `Proleasenet.Negocio/sn_clsCredito.vb`, `ActualizaLineaCredito` | Convierte el objeto a SQL mediante `Linea_creditoSentencias.ActualizarUno` y ejecuta la actualización. | La variante observada en la rama automática recibe la conexión del flujo de contrato. |

La evidencia confirma que existe una modalidad de alta automática que crea una
línea nueva, no una selección obligatoria del usuario. También confirma la
asociación de la línea con operaciones mediante `KLTOPERA` en el flujo de
administración de líneas. No demuestra todavía que todas las instalaciones o
todos los tipos de operación CD usen esa misma rama ni que la llamada desde
`GuardaInfo` comparta una única transacción con `ActualizaContrato`; ambas
condiciones deben verificarse antes del POST moderno.

### Valores observados en la rama de alta automática

El constructor `Linea_credito` y la sentencia de alta muestran los siguientes
campos escritos. La evidencia de valor es técnica y no sustituye la
confirmación del esquema físico completo:

| Columna | Origen observado | Valor/regla para CD | Estado |
|---|---|---|---|
| `LCR_FL_CVE` | `ObtenConsecutivo("KLINEA_CREDITO", ...)` | Nuevo consecutivo; no reutilizar por selección del usuario | Parcialmente confirmado |
| `PNA_FL_PERSONA` | cliente capturado | Persona seleccionada; debe pertenecer al tenant | Confirmado en la rama |
| `LCR_FL_ANALISTA` | usuario/analista del flujo | `LegacyUserCode` o equivalente Legacy, sujeto a longitud física | Parcial |
| `LCR_FE_REGISTRO` | fecha recibida por la rutina | Fecha de registro de línea | Parcial |
| `LCR_FE_VENCIMIENTO` | fecha calculada por la rutina | En una llamada observada se deriva de la fecha de operación más el plazo configurado | Parcial |
| `LCR_CL_MONEDA` | condición financiera | Moneda del contrato | Parcial |
| `LCR_NO_MTO_APROBADO` | monto del contrato/línea | En la rama automática observada se usa el monto financiado | Parcial |
| `LCR_NO_MTO_DISPUESTO` | cálculo de disposición | Inicializado en cero en la llamada observada | Parcial; confirmar regla de CD |
| `LCR_NO_MTO_DISPONIBLE` | cálculo de línea | Inicializado con la capacidad disponible de la línea | Parcial |
| `LCR_CL_TLINEA` | tipo de línea | La llamada observada usa el valor técnico 1; falta catálogo y regla funcional | Bloqueado |
| `LCR_FG_TASA_DFT` | selección/configuración de tasa | La llamada automática usa el valor técnico 0; falta confirmar significado | Bloqueado |
| `TAS_FL_CVE` | tasa de contrato | Puede ser nulo en la rutina Legacy cuando no hay tasa seleccionada | Bloqueado por regla CD |
| `LCR_NO_TASA_BASE` | cálculo/configuración de tasa | Parámetro de tasa base | Bloqueado |
| `LCR_NO_PUNTOS_ADIC` | cálculo/configuración de tasa | Puntos adicionales | Bloqueado |
| `LCR_NO_FACTOR` | cálculo financiero | Factor de la línea | Bloqueado |
| `LCR_NO_TASA_NOMINAL` | cálculo financiero | Tasa nominal | Bloqueado |
| `LCR_DS_COMPOSICION` | composición de la línea | Cadena técnica recibida por la rutina | Bloqueado por semántica |
| `LCR_DS_OBSERVACIONES` | observaciones Legacy | Valor recibido por la rutina | Bloqueado; no aceptar texto arbitrario sin contrato |
| `LCR_FG_STATUS` | estado de línea | La llamada automática usa 0 en el constructor observado; no está aprobado como estado de alta moderno | Bloqueado |
| `LCR_FE_ULTMOD` | fecha de modificación | La implementación observada usa fecha del reloj del proceso en una ruta; debe sustituirse por regla de fecha operativa si así lo exige el contrato moderno | Bloqueado |
| `USR_CL_CVE` | usuario Legacy | `LegacyUserCode` de la membresía seleccionada | Parcial |
| `LCR_NO_PLAZOCTO` | plazo del contrato | Plazo del contrato | Parcial |
| `LCR_NO_PLAZO_MINIMO` | regla de plazo | La rutina usa el plazo mínimo si es positivo y, en caso contrario, 1 | Parcial |
| `LCR_FG_MULTIMONEDA` | configuración monetaria | La rama observada usa 0 | Bloqueado por regla CD |
| `LCR_NO_TIPO_CAMBIO` | configuración monetaria | La rama observada usa 0 | Bloqueado |
| `LCR_NO_MTO_SALDO_INSOLUTO` | cálculo financiero | La rama observada usa 0 | Bloqueado |
| `TLC_FL_CVE` | tipo de cartera | Para el MVP CD se resuelve mediante `CLTOPERACION` y la relación `CD -> 2`; la variante histórica que usa 0 no es la regla moderna | Parcial; requiere validar catálogo activo |

Esta tabla no se presenta como el esquema completo de `KLINEA_CREDITO`: es la
lista de columnas que las fuentes consultadas muestran explícitamente en las
sentencias de alta. El constructor también distingue algunos parámetros
nullable (`LCR_NO_PLAZOCTO`, `PNA_FL_PERSONA` y `TAS_FL_CVE` en su firma), pero
la nulabilidad real debe prevalecer sobre esa firma.

### Semántica de creación y reutilización

La única modalidad de creación automática localizada para la ruta de contrato
reserva un nuevo consecutivo y construye una nueva línea. La evidencia
revisada no permite afirmar que siempre se cree una línea para cada contrato
CD en Toyota, ni descarta que otras ramas reutilicen una línea existente. La
ruta alternativa `If Not blnLinAut Then` reutiliza `intLineaCred`, recarga la
línea existente y actualiza sus montos; esa rama no debe confundirse con el MVP
aprobado y tampoco convierte `LCR_FL_CVE` en un campo seleccionable por el
usuario moderno.

`Reestructuras.vb` contiene otra creación de línea, pero corresponde a
reestructura y queda fuera del escenario CD nuevo. `sd_clsCesion.vb` también
crea líneas para cesión; no es evidencia aplicable al MVP.

### Transacción e idempotencia propuestas

El alta moderna debe usar una única transacción `Serializable` y bloquear la
identidad lógica del intento antes de reservar el consecutivo. El orden
propuesto, sujeto a confirmar con `GuardaInfo`, es:

1. autenticar, autorizar, validar tenant, membresía y `LegacyUserCode`;
2. validar readiness y catálogos del cliente/operación CD;
3. verificar una clave de idempotencia del intento de alta;
4. comprobar la habilitación de línea automática para la empresa y contrato;
5. reservar el consecutivo de `KLINEA_CREDITO` mediante el mecanismo Legacy;
6. insertar la línea y sus asociaciones de operación (`KLTOPERA`) dentro de la transacción;
7. ejecutar `spLsnetGeneraClaveContrato` dentro de la misma transacción;
8. insertar `KCONTRATO` con el `LCR_FL_CVE` generado;
9. ejecutar pagos, amortización, CAT, cargos y bitácora según la rama CD;
10. confirmar sólo si todas las operaciones terminan correctamente.

Un reintento con la misma clave debe devolver el resultado ya confirmado o un
conflicto controlado, nunca reservar una segunda línea. Un fallo debe revertir
la línea, `KLTOPERA`, su consecutivo, la clave de contrato, contrato, pagos,
amortización, cargos y bitácora que pertenezcan a la misma transacción.

### Esquema físico pendiente de KLINEA_CREDITO

Las fuentes Legacy disponibles no constituyen evidencia autoritativa de todas
las columnas, tipos, defaults, PK/FK, índices, triggers y CHECK constraints.
No se ejecutó SQL. Para cerrar la matriz se requieren consultas de sólo
lectura como las siguientes, sin seleccionar datos personales ni valores
financieros:

```sql
SELECT c.column_id, c.name, t.name AS type_name, c.max_length,
       c.precision, c.scale, c.is_nullable, c.is_identity,
       dc.definition AS default_definition
FROM sys.columns AS c
JOIN sys.tables AS tb ON tb.object_id = c.object_id
JOIN sys.types AS t ON t.user_type_id = c.user_type_id
LEFT JOIN sys.default_constraints AS dc ON dc.parent_object_id = c.object_id
    AND dc.parent_column_id = c.column_id
WHERE tb.name = 'KLINEA_CREDITO'
ORDER BY c.column_id;

SELECT i.name, i.is_unique, i.is_primary_key, ic.key_ordinal,
       c.name AS column_name
FROM sys.indexes AS i
JOIN sys.index_columns AS ic ON ic.object_id = i.object_id
    AND ic.index_id = i.index_id
JOIN sys.columns AS c ON c.object_id = ic.object_id
    AND c.column_id = ic.column_id
JOIN sys.tables AS tb ON tb.object_id = i.object_id
WHERE tb.name = 'KLINEA_CREDITO'
ORDER BY i.name, ic.key_ordinal;

SELECT fk.name, OBJECT_NAME(fk.parent_object_id) AS child_table,
       COL_NAME(fkc.parent_object_id, fkc.parent_column_id) AS child_column,
       OBJECT_NAME(fk.referenced_object_id) AS parent_table,
       COL_NAME(fkc.referenced_object_id, fkc.referenced_column_id) AS parent_column
FROM sys.foreign_keys AS fk
JOIN sys.foreign_key_columns AS fkc ON fkc.constraint_object_id = fk.object_id
WHERE fk.parent_object_id = OBJECT_ID('dbo.KLINEA_CREDITO');

SELECT tr.name, tr.is_disabled
FROM sys.triggers AS tr
WHERE tr.parent_id = OBJECT_ID('dbo.KLINEA_CREDITO');

SELECT cc.name, cc.definition
FROM sys.check_constraints AS cc
WHERE cc.parent_object_id = OBJECT_ID('dbo.KLINEA_CREDITO');

SELECT CCS_DS_NOMTABLA, CCT_DS_CAMPO, CCT_NO_CONSECUTIVO, ID_FL_CVE
FROM dbo.CCATCONSEC
WHERE CCS_DS_NOMTABLA = 'KLINEA_CREDITO';
```

La matriz de columnas NOT NULL de `KLINEA_CREDITO` queda bloqueada hasta
recibir ese resultado y la trazabilidad de la rama CD. No es seguro rellenar
las columnas faltantes con ceros por frecuencia, por la firma del constructor
o por una ruta de reestructura.

### Resultado frente al MVP CD

- Modalidad confirmada: existe alta automática con nuevo consecutivo; la
  reutilización pertenece a otra rama y no es una selección del usuario.
- `LCR_FL_CVE`: resultado interno obligatorio, no propiedad HTTP.
- Tablas involucradas observadas: `KLINEA_CREDITO`, `KLTOPERA`, `KCONTRATO` y
  las tablas posteriores de pagos, amortización, cargos y auditoría según la
  ruta de contrato.
- Procedimientos/métodos involucrados: `LineaAutomatica`,
  `ActualizaLineaUsuario`, `ActualizaLineaCredito`,
  `ActualizaOperacionesLinea` y `spLsnetGeneraClaveContrato`.
- Consecutivos: `KLINEA_CREDITO` usa `ObtenConsecutivo`; la numeración de
  `KCONTRATO` sigue usando el procedimiento de clave ya documentado.
- Bloqueos restantes: esquema físico completo de la línea, valores de sus
  NOT NULL para CD, catálogo de tipo de línea/cartera, asociación exacta de
  operaciones, frontera transaccional real de `GuardaInfo` y reglas de
  idempotencia.
- El MVP CD no está listo para implementar un POST seguro. La decisión de no
  pedir la línea al usuario sí queda incorporada; no sustituye la evidencia
  faltante de persistencia y de transacción.

## Estado de la evidencia SQL y frontera transaccional

En este turno no se recibió ni quedó accesible un resultado nuevo de
`sys.columns`, índices, claves, defaults, triggers o CHECK constraints para
`KLINEA_CREDITO` y `KLTOPERA`. Por tanto, la matriz física completa y el
conteo autoritativo de columnas `NOT NULL` continúan pendientes. Las fuentes
Legacy sólo permiten documentar las columnas que aparecen en las sentencias
de alta y no sustituyen la evidencia de `sys.columns`.

### Matriz física disponible

| Objeto | Evidencia disponible | Estado |
|---|---|---|
| `KLINEA_CREDITO` | Sentencias Legacy muestran `LCR_FL_CVE`, `PNA_FL_PERSONA`, campos de usuario, fechas, moneda, importes, tipo de línea, tasa, estado, plazo, multimoneda, tipo de cambio, saldo insoluto y `TLC_FL_CVE` en variantes de alta. La evidencia SQL confirma 29 columnas, 18 `NOT NULL`, PK en `LCR_FL_CVE`, sin defaults, triggers ni CHECK, y FK de persona. | Esquema físico confirmado; reglas de negocio de varias columnas siguen pendientes. |
| `KLTOPERA` | El código elimina asociaciones previas para `LCR_FL_CVE` y agrega pares `LCR_FL_CVE`/`TOP_CL_CVE`, con fecha de modificación y usuario. La evidencia SQL confirma 4 columnas, 3 `NOT NULL`, PK compuesta, sin defaults, triggers ni CHECK, y sin FK físicas confirmadas. | Esquema físico confirmado; fecha y usuario requieren regla de alta cerrada. |
| `CCATCONSEC` | El código usa `ObtenConsecutivo("KLINEA_CREDITO", ...)`. | El mecanismo está localizado; empresa/parámetros exactos y efecto de rollback requieren evidencia SQL y del adaptador. |

No se deben convertir las listas de columnas de las sentencias Legacy en una
matriz física: una columna omitida puede tener default, aceptar NULL o ser
asignada por otro procedimiento.

### Matriz de NOT NULL para CD

La siguiente matriz es deliberadamente conservadora. No se marca como
confirmada ninguna columna sólo por aparecer en el `INSERT`.

| Columna o grupo visible en la alta | Valor exacto CD | Origen | Validación requerida | Evidencia | Estado |
|---|---|---|---|---|---|
| `LCR_FL_CVE` | Nuevo consecutivo | `ObtenConsecutivo("KLINEA_CREDITO")` | Unicidad, pertenencia a persona y asociación a CD | `sd_clsCredito.ActualizaLineaUsuario`; rama automática de `sn_clsContratos` | Parcial |
| `PNA_FL_PERSONA` | Persona seleccionada | Cliente | Persona activa y alcance del tenant | Constructor de `Linea_credito` y sentencia de alta | Parcial |
| `LCR_FL_ANALISTA`, `USR_CL_CVE` | Usuario Legacy de la membresía | Actor de ejecución | `LegacyUserCode` presente y longitud física confirmada | Parámetros `strCveAnalCredito`/`strUser` | Parcial |
| Fechas de registro, vencimiento y última modificación | Fecha de operación o cálculo Legacy | Fecha operativa/cálculo | `CFECHA_OPERACION`, precisión y reglas de vencimiento | `ActualizaLineaUsuario` y constructor automático | Bloqueado |
| `LCR_CL_MONEDA` | Moneda compatible con CD | Contrato/catálogo | Catálogo activo y compatibilidad | Parámetro `shrtCveMoneda` | Bloqueado |
| Importes (`LCR_NO_MTO_APROBADO`, `LCR_NO_MTO_DISPUESTO`, `LCR_NO_MTO_DISPONIBLE`) | Monto financiado y saldos iniciales | Cálculo financiero | Fórmula CD, límites y consistencia | Parámetros de `ActualizaLineaUsuario` | Bloqueado |
| `LCR_CL_TLINEA`, `TLC_FL_CVE` | Tipo de línea y cartera resueltos por catálogos | `CLTOPERACION`/`CLINEA_CREDITO` | Vigencia y compatibilidad con CD | Constructor, relación de operación y catálogo | Parcial para `TLC_FL_CVE`; `LCR_CL_TLINEA` pendiente |
| Tasas (`TAS_FL_CVE`, `LCR_FG_TASA_DFT`, `LCR_NO_TASA_BASE`, `LCR_NO_PUNTOS_ADIC`, `LCR_NO_FACTOR`, `LCR_NO_TASA_NOMINAL`) | Depende de la configuración CD | Catálogo/cálculo | Regla de tasa ordinaria y nulabilidad real | Parámetros de `ActualizaLineaUsuario` | Bloqueado |
| `LCR_DS_COMPOSICION`, `LCR_DS_OBSERVACIONES` | Texto construido por Legacy | Constante/cálculo/usuario | Longitud, contenido permitido y ausencia de PII | Argumentos de la rutina | Bloqueado |
| `LCR_FG_STATUS`, `LCR_FG_MULTIMONEDA` | Valores técnicos observados | Constante/configuración | Significado funcional y estado inicial aprobado | Rama automática/variantes de INSERT | Bloqueado |
| `LCR_NO_PLAZOCTO`, `LCR_NO_PLAZO_MINIMO` | Plazo CD y mínimo aplicable | Operación/cálculo | Plazo permitido y regla de valor mínimo | Constructor y `ActualizaLineaUsuario` | Parcial |
| `LCR_NO_TIPO_CAMBIO`, `LCR_NO_MTO_SALDO_INSOLUTO` | Valores monetarios derivados | Cálculo | Regla para moneda nacional/multimoneda | Variante de alta con esos campos | Bloqueado |

El listado exacto de todas las columnas `NOT NULL` no puede cerrarse sin el
resultado físico solicitado. En particular, no es válido afirmar que las
columnas omitidas por una variante sean nullable ni asignarles cero.

### Respuestas transaccionales

| Pregunta | Conclusión actual | Evidencia/pendiente |
|---|---|---|
| ¿La línea usa el mismo `_Tran` que `KCONTRATO`? | No confirmado. | `sn_clsContratos` pasa `objConexion` a `ActualizaLineaCredito`; el código localizado no demuestra que sea el mismo `_Tran` de `sd_clsContratoPropuesta.ActualizaContrato`. |
| ¿`KLTOPERA` está en la misma transacción? | Confirmado sólo para la rutina `ActualizaLineaUsuario` cuando recibe o crea `objTran`; no confirmado para la rama automática del contrato. | La rutina ejecuta el borrado/inserción de `KLTOPERA` antes de su commit; falta trazar `ActualizaOperacionesLinea` en la rama CD. |
| ¿`ObtenConsecutivo` participa en la transacción? | Parcial. | `ActualizaLineaUsuario` lo invoca con `objTran`; la rama automática usa `Utils.ObtenConsecutivo(..., objConexion)`, sin evidencia del objeto transaccional. |
| ¿Rollback revierte el consecutivo? | No confirmado. | Requiere leer la implementación de `ObtenConsecutivo`/`CCATCONSEC` y una prueba controlada; no se ejecutará SQL en esta etapa. |
| ¿Puede quedar una línea huérfana si falla `ActualizaContrato`? | Sí, es un riesgo plausible mientras no se pruebe la transacción compartida. | La llamada automática crea la línea antes de continuar con contrato y usa una abstracción de conexión distinta a la rutina transaccional explícita. |
| ¿Cuándo comprobar idempotencia? | Antes de reservar `LCR_FL_CVE`, y volver a verificar bajo el mismo bloqueo antes de insertar. | Evita duplicar línea, contrato, pagos y cargos ante reintentos concurrentes. |

La implementación moderna debe exigir evidencia de una transacción única antes
de adoptar esta secuencia. Si el Legacy no comparte el `_Tran`, uCredit debe
mantener la operación bloqueada o encapsular explícitamente línea, asociaciones,
contrato, pagos, amortización, cargos, bitácora y consecutivos en una única
transacción compatible; no debe aceptar una línea huérfana como estado válido.

### Conteo y decisión

- Columnas obligatorias de `KLINEA_CREDITO`: no determinable sin `sys.columns`.
- Columnas confirmadas con valor técnico: sólo las asignaciones visibles en las
  sentencias Legacy; no equivalen a columnas físicamente `NOT NULL`.
- Columnas bloqueadas: todas las columnas físicamente `NOT NULL` cuyo origen,
  default o regla CD no estén confirmados; el total no puede calcularse sin
  el esquema autoritativo.
- Tablas escritas observadas: `KLINEA_CREDITO` y `KLTOPERA`; posteriormente
  `KCONTRATO` y tablas de pagos/amortización/cargos según el flujo.
- Consecutivos afectados: `KLINEA_CREDITO` y la clave de contrato; cualquier
  consecutivo adicional debe confirmarse en la rama CD.
- Riesgo principal: línea o asociación huérfana si el alta de línea no comparte
  la transacción del contrato o si el consecutivo no revierte.
- El MVP CD no puede pasar todavía a diseño técnico implementable. La decisión
  funcional de generación automática queda aprobada, pero faltan los
  metadatos SQL y la prueba/confirmación de la frontera transaccional real.

## Evidencia SQL adicional: línea automática y `KLTOPERA`

### Esquema físico confirmado

| Objeto | Columnas | NOT NULL | Nullable | PK | FK | Defaults/triggers/CHECK |
|---|---:|---:|---:|---|---|---|
| `KLINEA_CREDITO` | 29 | 18 | 11 | `LCR_FL_CVE` | `PNA_FL_PERSONA → CPERSONA.PNA_FL_PERSONA` | Sin defaults, triggers ni CHECK confirmados |
| `KLTOPERA` | 4 | 3 | 1 | (`LCR_FL_CVE`, `TOP_CL_CVE`) | Ninguna FK física confirmada | Sin defaults, triggers ni CHECK confirmados |

`CCATCONSEC` contiene actualmente dos filas con el mismo valor reportado:

| Tabla lógica | Empresa | Consecutivo | Campo | ID |
|---|---:|---:|---|---|
| `KLINEA_CREDITO` | 0 | 861945 | `LCR_FL_CVE` | `LCR_FL_CVE` |
| `KLTOPERA` | 0 | 861945 | `LCR_FL_CVE` | `LCR_FL_CVE` |

La coincidencia numérica no demuestra que ambas filas deban incrementarse.
La rama automática llama `Utils.ObtenConsecutivo("KLINEA_CREDITO", objConexion)`
y no llama a `ObtenConsecutivo("KLTOPERA", ...)`. `KLTOPERA` recibe el mismo
identificador generado para la línea; no reserva un identificador propio.

### Matriz de las 18 columnas NOT NULL de `KLINEA_CREDITO`

Los tipos físicos deben conservarse desde la evidencia `sys.columns` recibida;
no se han inferido desde VB.NET. La expresión y los valores se documentan
separadamente para no convertir una frecuencia histórica en regla.

| Columna | Expresión/valor del INSERT Legacy | Argumento upstream | Origen | Valor CD | Validación | Evidencia | Estado |
|---|---|---|---|---|---|---|---|
| `LCR_FL_CVE` | valor del consecutivo usado en `VALUES` | `intCveLCredito` | consecutivo | nuevo valor de `CCATCONSEC` | PK y no reutilización del intento | `sn_clsContratos`, `Utils.ObtenConsecutivo` | Confirmado técnicamente |
| `LCR_FE_REGISTRO` | `dateFecRegistro` formateada | `dateFecRegistro` | fecha de línea | fecha operativa o fecha de registro Legacy confirmada por rama | fecha válida y precisión física | `sd_clsCredito.ActualizaLineaUsuario` | Parcial |
| `LCR_FE_VENCIMIENTO` | `dateFecVencimiento` formateada | `dateFecVencimiento` | cálculo de línea | plazo de línea según CD | vencimiento posterior al registro | misma rutina | Parcial |
| `LCR_NO_PLAZOCTO` | `intPlazoMaxCto` | `intPlazoMaxCto` | operación/cálculo | plazo CD | rango y compatibilidad con esquema | sentencia de alta | Parcial |
| `LCR_NO_PLAZO_MINIMO` | `IIf(intPlazoMinimo > 0, intPlazoMinimo, 1)` | `intPlazoMinimo` | regla Legacy | mínimo recibido; `1` sólo si la regla Legacy aplica | rango y relación con plazo | sentencia de alta | Parcial |
| `LCR_CL_MONEDA` | `shrtCveMoneda` | `intMoneda`/`shrtCveMoneda` | catálogo/operación | moneda CD aprobada por catálogo | activo y compatible | constructor y `ActualizaLineaUsuario` | Parcial |
| `LCR_NO_MTO_APROBADO` | `dblMtoAprobado` | monto aprobado | cálculo financiero | monto de línea CD | decimal, límites y no negativo | sentencia de alta | Pendiente funcional |
| `LCR_NO_MTO_DISPUESTO` | `intMtoDispuesto` | monto dispuesto | cálculo financiero | valor inicial Legacy, no asumir cero fuera de evidencia | consistencia con aprobado/disponible | sentencia de alta | Pendiente |
| `LCR_NO_MTO_DISPONIBLE` | `dblMtoDisponible` | disponible | cálculo financiero | disponible inicial CD | `aprobado - dispuesto` según regla cerrada | sentencia de alta | Pendiente |
| `LCR_CL_TLINEA` | `shrtCveTLinea` | tipo de línea | catálogo | no usar 1 sólo por frecuencia | activo y compatible con CD | sentencia de alta | Pendiente |
| `LCR_FG_TASA_DFT` | `shrtTDefault` | indicador de tasa default | configuración/tasa | no usar 1 sólo por frecuencia | valores permitidos y relación con tasa | sentencia de alta | Pendiente |
| `LCR_NO_TASA_BASE` | `dblTBase` | tasa base | cálculo/catálogo | tasa base CD | rango y vigencia | sentencia de alta | Pendiente |
| `LCR_NO_PUNTOS_ADIC` | `shrtPuntosAdic` | puntos adicionales | cálculo/catálogo | puntos CD | rango y compatibilidad | sentencia de alta | Pendiente |
| `LCR_NO_FACTOR` | `dblFactor` | factor | cálculo financiero | factor CD | fórmula confirmada | sentencia de alta | Pendiente |
| `LCR_NO_TASA_NOMINAL` | `dblTNominal` | tasa nominal | cálculo financiero | tasa nominal CD | fórmula, rango y precisión | sentencia de alta | Pendiente |
| `LCR_FG_STATUS` | `shrtStatus` | estado de línea | estado/configuración | no convertir la frecuencia `1` en regla sin catálogo | estado inicial autorizado | sentencia de alta | Pendiente |
| `LCR_FE_ULTMOD` | `Format$(Now, "yyyy-MM-dd")` en la variante observada | reloj de proceso Legacy | fecha de modificación | debe definirse con la política de fecha operativa moderna | precisión y fuente autorizada | `sd_clsCredito.vb` | Pendiente; no replicar `Now` automáticamente |
| `TLC_FL_CVE` | `0` en una variante histórica del INSERT, pero el MVP resuelve `2` | relación operación/catálogo | `CLTOPERACION.TOP_CL_CVE = 'CD'` | `2` | fila activa `TLC_FG_STATUS = 1`; rechazar si falta relación/catálogo | `CLTOPERACION` y `CLINEA_CREDITO` | Parcial; regla CD confirmada |

Aunque `PNA_FL_PERSONA` es nullable físicamente en la tabla, el MVP lo trata
como obligatorio: debe ser la persona seleccionada, activa y dentro del
tenant. Las once columnas nullable no se rellenan automáticamente; su uso
debe documentarse por separado antes de implementar.

### Matriz de `KLTOPERA`

| Columna | Regla CD | Origen | Validación | Estado |
|---|---|---|---|---|
| `LCR_FL_CVE` | Identificador generado internamente para la línea | resultado de `KLINEA_CREDITO` | la línea debe existir y pertenecer a la persona | Confirmado |
| `TOP_CL_CVE` | `CD` | operación seleccionada y validada | operación activa, autorizada y compatible | Confirmado para el MVP |
| `LTP_FE_ULTMOD` | fecha de operación o fuente Legacy que se confirme | `ActualizaOperacionesLinea`/`InsertaLtOpera` | fecha operativa única y precisión física | Parcial |
| `USR_CL_CVE` | `LegacyUserCode` | membresía activa seleccionada | longitud y pertenencia al tenant | Parcial |

`ActualizaOperacionesLinea` primero elimina asociaciones de la línea y después
inserta una fila por operación. Para una línea nueva del MVP debe producir una
sola relación para `CD`; no se deben trasladar asociaciones históricas.

### `TLC_FL_CVE`

El campo aparece en `CLINEA_CREDITO`, `CLTEQUIPO`, `CLTOPERACION` y
`KLINEA_CREDITO`. La evidencia nueva confirma:

- `TLC_FL_CVE = 2`;
- `TLC_DS_NOMBRE = LINEA CD`;
- `TLC_FG_STATUS = 1`;
- `TLC_NO_DIAS = 180`;
- `TLC_FG_TIPOB = 1`;
- `CLTOPERACION.TOP_CL_CVE = 'CD'` se relaciona con `TLC_FL_CVE = 2`.

Para el MVP, el frontend no envía `TLC_FL_CVE`; el backend debe resolverlo por
`TOP_CL_CVE = 'CD'` y validar después la fila activa de `CLINEA_CREDITO`. No se
usa `0`, aunque sea frecuente históricamente. `TLC_NO_DIAS = 180` queda como
dato de catálogo; todavía no se afirma que `LCR_FE_VENCIMIENTO` se calcule
sumando 180 días porque esa operación no está confirmada en el código Legacy.
Si falta la relación o el catálogo activo, el alta debe fallar de forma
controlada antes de reservar el consecutivo o escribir datos.

Se conservan además las siguientes consultas de sólo lectura para cerrar el
resto del catálogo y sus relaciones:

```sql
SELECT c.column_id, c.name, t.name AS type_name, c.max_length,
       c.precision, c.scale, c.is_nullable, dc.definition
FROM sys.columns AS c
JOIN sys.tables AS tb ON tb.object_id = c.object_id
JOIN sys.types AS t ON t.user_type_id = c.user_type_id
LEFT JOIN sys.default_constraints AS dc ON dc.parent_object_id = c.object_id
    AND dc.parent_column_id = c.column_id
WHERE tb.name = 'CLINEA_CREDITO'
ORDER BY c.column_id;

SELECT i.name, i.is_unique, i.is_primary_key, c.name AS column_name,
       ic.key_ordinal
FROM sys.indexes AS i
JOIN sys.index_columns AS ic ON ic.object_id = i.object_id
    AND ic.index_id = i.index_id
JOIN sys.columns AS c ON c.object_id = ic.object_id
    AND c.column_id = ic.column_id
JOIN sys.tables AS tb ON tb.object_id = i.object_id
WHERE tb.name = 'CLINEA_CREDITO'
ORDER BY i.name, ic.key_ordinal;

SELECT fk.name, OBJECT_NAME(fk.parent_object_id) AS child_table,
       COL_NAME(fkc.parent_object_id, fkc.parent_column_id) AS child_column,
       OBJECT_NAME(fk.referenced_object_id) AS parent_table,
       COL_NAME(fkc.referenced_object_id, fkc.referenced_column_id) AS parent_column
FROM sys.foreign_keys AS fk
JOIN sys.foreign_key_columns AS fkc ON fkc.constraint_object_id = fk.object_id
WHERE fk.parent_object_id = OBJECT_ID('dbo.CLINEA_CREDITO');

SELECT tr.name, tr.is_disabled
FROM sys.triggers AS tr
WHERE tr.parent_id = OBJECT_ID('dbo.CLINEA_CREDITO');

SELECT cc.name, cc.definition
FROM sys.check_constraints AS cc
WHERE cc.parent_object_id = OBJECT_ID('dbo.CLINEA_CREDITO');

SELECT TOP_CL_CVE, TLC_FL_CVE, TOP_FG_STATUS, TOP_FG_PP, EMP_FL_CVE
FROM dbo.CLTOPERACION
WHERE TOP_CL_CVE = 'CD';

SELECT TLC_FL_CVE, COUNT(*) AS Total
FROM dbo.CLINEA_CREDITO
GROUP BY TLC_FL_CVE
ORDER BY TLC_FL_CVE;
```

### Consecutivos y frontera transaccional

`Utils.ObtenConsecutivo` recibe sólo el nombre de tabla y un objeto
`ManejaBD`. Para la ruta observada actualiza la fila cuyo
`CCS_DS_NOMTABLA` coincide con `KLINEA_CREDITO`, lee el valor actualizado y lo
devuelve. No actualiza la fila de `KLTOPERA`. El código no muestra
`UPDLOCK`/`HOLDLOCK` explícitos; la serialización depende de la operación SQL,
la conexión y la transacción del objeto `ManejaBD`.

Por tanto:

1. El consecutivo que la rama automática incrementa es `KLINEA_CREDITO`.
2. La fila `KLTOPERA` con el mismo valor no se incrementa; sólo se reutiliza
   el `LCR_FL_CVE` generado.
3. La llamada automática pasa `objConexion` a `ActualizaLineaCredito` y a
   `ActualizaOperacionesLinea`, pero el fragmento localizado no prueba que
   `objConexion` tenga el mismo `_Tran` que el INSERT de `KCONTRATO`.
4. `ActualizaLineaUsuario` sí tiene una transacción propia y ejecuta sus
   escrituras de línea y `KLTOPERA` antes de commit; no demuestra la frontera
   del flujo `SDInsertaContrato`.
5. No hay evidencia suficiente para confirmar que el rollback del contrato
   revierta el incremento de `CCATCONSEC`; debe probarse en una transacción
   controlada antes del POST.
6. Mientras esa frontera no esté confirmada, existe riesgo de línea huérfana
   si el contrato falla después de la línea o de `KLTOPERA`.

El diseño moderno debe envolver línea, relación, contrato, pagos,
amortización, cargos, auditoría y el único registro de `CCATCONSEC` de línea
en una sola transacción. Debe comprobar idempotencia antes de reservar el
consecutivo y repetir la comprobación bajo bloqueo dentro de la misma
transacción.

### Resultado actualizado del MVP CD

- Campos obligatorios de `KLINEA_CREDITO`: 18.
- Confirmados completamente dentro de las 18 columnas físicamente NOT NULL:
  1 (`LCR_FL_CVE`). `PNA_FL_PERSONA` queda fuera de ese conteo por ser
  nullable física, pero es obligatorio por regla de aplicación del MVP y debe
  corresponder al cliente seleccionado.
- Parciales: fechas/plazos, moneda, usuario y asociación operativa; sus
  valores exactos de negocio aún requieren cierre.
- Pendientes o bloqueados: las restantes columnas NOT NULL, especialmente
  tipo de línea, tasas, estado, importes y cartera. `TLC_FL_CVE` ya tiene una
  regla funcional para CD (`CLTOPERACION` resuelve `2` y `CLINEA_CREDITO` debe
  estar activa), aunque la validación debe ejecutarse antes de reservar el
  consecutivo.
- `KLTOPERA`: relación `LCR_FL_CVE`/`CD` confirmada; fecha y usuario requieren
  confirmar fuente exacta.
- Consecutivo a incrementar: sólo `KLINEA_CREDITO`; no incrementar
  `KLTOPERA` sin nueva evidencia.
- Líneas compartidas: excepciones históricas; no replicarlas en el MVP.
- Bloqueos restantes: tipos y valores de línea, transacción compartida,
  rollback del consecutivo, cierre de las columnas
  NOT NULL y prueba de idempotencia.
- El MVP CD aún no puede pasar a implementación segura.
## Revisión adicional de la línea automática: montos, moneda, plazo y tasas

Esta revisión incorpora la evidencia agregada sin convertir frecuencias en
defaults. Los valores actuales de `KLINEA_CREDITO` pueden haber cambiado
después del alta; por ello la fuente autoritativa sigue siendo la rama
automática y la asignación que llega a `sd_clsCredito.ActualizaLineaUsuario`.

### Hechos confirmados por la sentencia de alta

En `sdLsenet/sd_clsCredito.vb`, `ActualizaLineaUsuario`, caso `intCveLCredito
= 0`, el `INSERT` recibe explícitamente `dateFecRegistro`,
`dateFecVencimiento`, `shrtCveMoneda`, `dblMtoAprobado`,
`intMtoDispuesto`, `dblMtoDisponible`, `shrtCveTLinea`, `shrtTDefault`,
`dblTBase`, `shrtPuntosAdic`, `dblFactor`, `dblTNominal`,
`intPlazoMaxCto` e `intPlazoMinimo`. La misma rutina:

- obtiene `LCR_FL_CVE` mediante `ObtenConsecutivo("KLINEA_CREDITO", ...)`;
- normaliza `LCR_NO_PLAZO_MINIMO` a `1` cuando `intPlazoMinimo` no es positivo;
- escribe `LCR_FG_MULTIMONEDA`, `LCR_NO_TIPO_CAMBIO`,
  `LCR_NO_MTO_SALDO_INSOLUTO` y `TLC_FL_CVE` con `0` en una variante del
  `INSERT`; esos literales no se promueven automáticamente a regla moderna;
- usa `TAS_FL_CVE = NULL` cuando `shrtCveTasa = 0` en la variante donde la
  tasa es nullable;
- usa `Format$(Now, "yyyy-MM-dd")` para `LCR_FE_ULTMOD` en la ruta observada.

La sentencia confirma el destino y los argumentos, pero no demuestra por sí
sola cómo la rama automática calcula los argumentos upstream. En particular,
no se encontró todavía en el fragmento revisado una asignación directa que
demuestre que `dateFecRegistro` y `dateFecVencimiento` provienen de
`CFECHA_OPERACION`, ni una fórmula que use `TLC_NO_DIAS`.

### Montos

La igualdad observada en los grupos agregados es:

`LCR_NO_MTO_APROBADO = LCR_NO_MTO_DISPUESTO + LCR_NO_MTO_DISPONIBLE`.

Se documenta como invariante observada y como validación candidata. Sigue
parcial hasta localizar en la rama automática el cálculo que asigna los tres
argumentos y confirmar si la igualdad aplica antes del `INSERT`, después de
una actualización o sólo para el subconjunto analizado. No se usará la
distribución para fijar valores iniciales.

### Moneda

La distribución muestra coincidencia entre `LCR_CL_MONEDA` y
`CTO_CL_MONEDA`, pero la regla sólo quedará confirmada para el MVP cuando el
código de la rama automática muestre que `shrtCveMoneda` se copia del valor
de moneda del contrato. La sentencia de `ActualizaLineaUsuario` sí recibe
`shrtCveMoneda` y lo inserta en `LCR_CL_MONEDA`; el origen contractual aún
requiere esa trazabilidad upstream.

### Plazo y vencimiento

Para `TLC_FL_CVE = 2`, `LCR_NO_PLAZO_MINIMO = 1` y los plazos observados son
variables. Esto no prueba una fórmula. El código localizado confirma que:

- `LCR_NO_PLAZOCTO` recibe `intPlazoMaxCto`;
- `LCR_NO_PLAZO_MINIMO` recibe `intPlazoMinimo` o `1` si éste no es positivo;
- `LCR_FE_REGISTRO` y `LCR_FE_VENCIMIENTO` reciben parámetros ya calculados,
  `dateFecRegistro` y `dateFecVencimiento`;
- no queda confirmado que `LCR_FE_VENCIMIENTO` use `TLC_NO_DIAS = 180`,
  `DATEADD`, meses, días naturales o `CFECHA_OPERACION`;
- existen rutas posteriores que pueden actualizar el vencimiento, por lo que
  el estado actual no representa necesariamente el valor inicial.

### Fechas

La coincidencia predominante entre `LCR_FE_REGISTRO` y
`CTO_FE_GENERACION` es evidencia auxiliar. Falta localizar la asignación de
`dateFecRegistro` en la rama automática para confirmar si ambos parten de
`CFECHA_OPERACION`. No se autoriza `GETDATE` ni `DateTime.Now` como sustituto.
Además, la rutina revisada usa el reloj del proceso para `LCR_FE_ULTMOD`, por
lo que esa columna permanece pendiente de una regla moderna explícita.

### Tasas

La rama de persistencia copia los argumentos `shrtTDefault`, `shrtCveTasa`,
`dblTBase`, `shrtPuntosAdic`, `dblFactor` y `dblTNominal` a las columnas de
la línea. La evidencia de `CTASA` cierra la configuración técnica del MVP,
pero no autoriza a recibir el identificador desde el navegador:

- la configuración activa de tasa fija ordinaria en pesos es la fila técnica
  actualmente identificada como `TAS_FL_CVE = 1`, con `TAS_DS_TASA = FIJA
  PESOS`, `TAS_FG_TTASA = 1`, `TAS_CL_MONEDA = 1`, `TAS_FG_STATUS = 1` y
  `TAS_FG_REVISION = 0`;
- el backend debe resolverla por tipo de tasa, moneda y operación, y volver a
  validar todos esos campos en `CTASA`; no debe confiar en un `TAS_FL_CVE`
  enviado por el frontend ni sustituir la tasa nominal anual;
- `TAS_FL_CVE = 7`, `8`, `2`, `5`, `6`, `9`, `3` y `4` quedan fuera de este
  MVP por las restricciones funcionales recibidas;
- si la consulta no devuelve exactamente una configuración activa compatible,
  el futuro POST debe responder `422 contract_rate_configuration_required`
  antes de reservar consecutivos o escribir.

La selección del catálogo queda confirmada para el MVP; la asignación final
de sus parámetros a la rama automática aún debe verificarse en el caller de
`ActualizaLineaUsuario`. Por tanto:

| Columna | Lo que queda confirmado | Lo que falta confirmar |
|---|---|---|
| `LCR_FG_TASA_DFT` | recibe `shrtTDefault`; el MVP propone `1` | confirmar asignación automática y significado Legacy |
| `TAS_FL_CVE` | se resuelve dinámicamente a la configuración activa fija en pesos; la evidencia actual identifica `1` | confirmar caller y validación duplicada en contrato/línea |
| `LCR_NO_TASA_BASE` | el MVP propone `0` | confirmar que la rama fija no lo deriva de otro catálogo |
| `LCR_NO_PUNTOS_ADIC` | el MVP propone `0` | confirmar que no existe componente adicional |
| `LCR_NO_FACTOR` | el MVP propone `0` | confirmar que no existe cálculo alterno |
| `LCR_NO_TASA_NOMINAL` | tasa nominal anual capturada por usuario | validar límites, precisión y copia al contrato/línea |

No se hardcodean `1`, `0` ni una tasa nominal por frecuencia histórica.

### Estado actualizado de las 18 columnas NOT NULL

| Estado | Total | Criterio |
|---|---:|---|
| Confirmadas | 1 | `LCR_FL_CVE`, generado por el consecutivo de la línea |
| Parciales | 16 | La política MVP propone valores para tipo de línea, tasa fija, estado y montos/tasas, pero falta verificar la asignación exacta en la rama automática |
| Bloqueadas | 1 | `LCR_FE_ULTMOD` entra en conflicto con el `Now` observado y aún no está demostrada su sustitución por `CFECHA_OPERACION` |
| **Total** | **18** | |

La columna bloqueada es `LCR_FE_ULTMOD`. Las parciales son
`LCR_FE_REGISTRO`, `LCR_FE_VENCIMIENTO`, `LCR_CL_MONEDA`,
`LCR_NO_MTO_APROBADO`, `LCR_NO_MTO_DISPUESTO`, `LCR_NO_MTO_DISPONIBLE`,
`USR_CL_CVE`, `LCR_NO_PLAZOCTO`, `LCR_NO_PLAZO_MINIMO`, `LCR_CL_TLINEA`,
`LCR_FG_TASA_DFT`, `LCR_NO_TASA_BASE`, `LCR_NO_PUNTOS_ADIC`, `LCR_NO_FACTOR`,
`LCR_NO_TASA_NOMINAL` y `LCR_FG_STATUS`.

### Bloqueos agrupados y fragmentos aún faltantes

1. **Fechas y vencimiento:** falta el caller que asigna
   `dateFecRegistro`/`dateFecVencimiento` y la rutina que calcula el
   vencimiento; no basta `TLC_NO_DIAS = 180`.
2. **Plazos:** falta el origen de `intPlazoMaxCto` e `intPlazoMinimo` para la
   operación CD y confirmar cambios posteriores al alta.
3. **Montos:** falta el cálculo upstream de los tres importes y la
   confirmación de la igualdad como invariante de escritura.
4. **Tasas:** falta el evento de la pestaña Tasa y sus consultas/cálculos para
   los seis valores de tasa.
5. **Estado y tipo de línea:** falta la regla de `LCR_CL_TLINEA` y el estado
   inicial autorizado de `LCR_FG_STATUS`; `TLC_FL_CVE` sí está resuelto para
   CD como `2`, sujeto a validación activa previa.
6. **Transacción y consecutivo:** falta probar en código/adaptador que la
   rama automática usa el mismo objeto transaccional para línea, operación y
   contrato y que el incremento se revierte con rollback.

Con la evidencia actual, la línea automática no puede diseñarse todavía con
seguridad para un POST CD: el enlace `CD -> TLC_FL_CVE 2` está confirmado,
pero las tasas, estado, tipo de línea, fechas y cálculos financieros aún
requieren trazabilidad de la rama Legacy.

## Política propuesta para el MVP CD: línea simple de tasa fija

La siguiente política queda documentada como propuesta limitada a `TOP_CL_CVE
= 'CD'`, moneda `1`, una línea nueva por contrato, sin subsidio, PBA, RUSH,
revisión, tasa variable, multimóneda ni reutilización de líneas. No se
implementa todavía ni convierte por sí sola los valores en defaults globales:

| Campo | Propuesta MVP | Estado de evidencia |
|---|---|---|
| `LCR_CL_TLINEA` | `1` | Pendiente de confirmar el catálogo/regla en el caller automático |
| `LCR_FG_TASA_DFT` | `1` | Pendiente de confirmar la asignación `shrtTDefault` |
| `LCR_NO_TASA_BASE` | `0` | Propuesta; confirmar en la rama fija |
| `LCR_NO_PUNTOS_ADIC` | `0` | Propuesta; confirmar en la rama fija |
| `LCR_NO_FACTOR` | `0` | Propuesta; confirmar en la rama fija |
| `LCR_NO_TASA_NOMINAL` | Tasa nominal anual capturada por el usuario | Pendiente de validación de rango, precisión y propagación |
| `LCR_FG_STATUS` | `1` | Pendiente de confirmar el estado inicial de alta |
| `LCR_FE_ULTMOD` | `CFECHA_OPERACION` | Bloqueada hasta eliminar la discrepancia con `Now` observado |

Para `KLINEA_CREDITO.TAS_FL_CVE` y `KCONTRATO.TAS_FL_CVE`, el contrato
moderno no recibe un identificador libre. Resuelve dinámicamente la
configuración de tasa fija ordinaria en pesos y valida `CTASA` con:
`TAS_FG_STATUS = 1`, `TAS_FG_TTASA = 1`, `TAS_CL_MONEDA = 1` y
`TAS_FG_REVISION = 0`. La evidencia actual identifica `TAS_FL_CVE = 1`, pero
ese valor no se hardcodea como entrada: es el resultado vigente del catálogo.
La tasa nominal anual es un valor distinto y proviene del usuario.

La política de montos propuesta para una línea nueva exclusiva es:

`LCR_NO_MTO_APROBADO = monto a financiar`,
`LCR_NO_MTO_DISPUESTO = 0`,
`LCR_NO_MTO_DISPONIBLE = LCR_NO_MTO_APROBADO`.

Debe cumplirse la igualdad aprobada como invariante de escritura, pero la
asignación queda pendiente hasta confirmar el caller automático y distinguir
el alta de modificaciones/disposiciones históricas.

La fórmula de `LCR_FE_VENCIMIENTO` continúa pendiente: no se asume
`DATEADD(DAY, 180, ...)`, meses naturales ni otra operación a partir de
`TLC_NO_DIAS = 180`. También debe confirmarse que `LCR_FE_REGISTRO` y
`LCR_FE_ULTMOD` se alimentan de `CFECHA_OPERACION` y que no se utiliza `Now`.

### Decisión de preparación

La configuración de `CTASA` y la relación `CD -> TLC_FL_CVE = 2` ya permiten
definir las consultas dinámicas de catálogos. La línea automática completa no
está lista para diseño seguro del POST mientras permanezcan pendientes la
fórmula de vencimiento, la asignación exacta de montos, la confirmación de
tipo/estado de línea, la propagación del nominal y la frontera transaccional
del consecutivo.

## Cierre aprobado de `KLINEA_CREDITO` para el MVP CD

Esta sección es la referencia vigente para el alcance limitado a `TOP_CL_CVE
= 'CD'`, moneda nacional, tasa fija, una línea nueva por contrato, sin
propuesta, subsidio, PBA, RUSH, seguros financiados, revisión, tasa variable,
multimóneda ni reutilización de líneas. No generaliza estas decisiones a otras
operaciones.

### Fecha operativa y frontera de la transacción

La aplicación moderna debe obtener exactamente una fila válida de
`CFECHA_OPERACION` al inicio de una única transacción `Serializable`. Esa
fecha se reutiliza sin consultar nuevamente el reloj del servidor para:
`KLINEA_CREDITO`, `KLTOPERA` y `KCONTRATO`. Si no existe exactamente una fecha
válida, la operación falla antes de reservar cualquier consecutivo.

No se debe reproducir `Now`, `DateTime.Now` ni `GETDATE`. La fecha de
vencimiento aprobada para el MVP es:

`LCR_FE_VENCIMIENTO = DATEADD(MONTH, 6, LCR_FE_REGISTRO)`.

Las excepciones históricas observadas no cambian la regla de alta nueva.
`TLC_NO_DIAS = 180` se valida como dato del catálogo, pero no participa en el
`DATEADD` del MVP.

### Matriz definitiva de las 18 columnas `NOT NULL`

| Columna | Valor exacto MVP CD | Origen y asignación | Evidencia | Estado |
|---|---|---|---|---|
| `LCR_FL_CVE` | Consecutivo nuevo de `KLINEA_CREDITO` | `ObtenConsecutivo` dentro de la transacción | Código Legacy de alta automática | Confirmada por evidencia Legacy |
| `LCR_FE_REGISTRO` | Fecha única de `CFECHA_OPERACION` | Fecha operativa tomada al inicio | Decisión moderna y regla de fecha | Aprobada MVP |
| `LCR_FE_VENCIMIENTO` | `DATEADD(MONTH, 6, fechaOperativa)` | Cálculo de línea | Decisión funcional; 161221 coincidencias históricas son auxiliares | Aprobada MVP |
| `LCR_NO_PLAZOCTO` | Plazo del contrato | Captura/validación del contrato CD | `intPlazoMaxCto` y control de plazo Legacy | Aprobada MVP; catálogo/rango pendiente |
| `LCR_NO_PLAZO_MINIMO` | `1` | Constante de línea CD | Regla aprobada; Legacy usa `1` cuando no hay mínimo positivo | Aprobada MVP |
| `LCR_CL_MONEDA` | `1` | Moneda nacional del contrato | Restricción MVP y parámetro `shrtCveMoneda` | Aprobada MVP |
| `LCR_NO_MTO_APROBADO` | Monto a financiar | Derivado del contrato | Regla moderna aprobada; asignación Legacy exacta pendiente | Aprobada MVP |
| `LCR_NO_MTO_DISPUESTO` | `0` | Constante de alta nueva | Regla moderna aprobada | Aprobada MVP |
| `LCR_NO_MTO_DISPONIBLE` | Monto a financiar | Derivado del monto aprobado | Regla moderna aprobada | Aprobada MVP |
| `LCR_CL_TLINEA` | `1` | Tipo de línea del MVP | Política moderna; catálogo funcional Legacy aún debe validarse | Aprobada MVP, validación pendiente |
| `LCR_FG_TASA_DFT` | `1` | Constante de tasa fija | Política moderna y parámetro `shrtTDefault` | Aprobada MVP, asignación Legacy pendiente |
| `LCR_NO_TASA_BASE` | `0` | Constante de tasa fija del MVP | Política moderna | Aprobada MVP |
| `LCR_NO_PUNTOS_ADIC` | `0` | Constante de tasa fija del MVP | Política moderna | Aprobada MVP |
| `LCR_NO_FACTOR` | `0` | Constante de tasa fija del MVP | Política moderna | Aprobada MVP |
| `LCR_NO_TASA_NOMINAL` | Tasa nominal anual capturada | Usuario; se valida y propaga también a `KCONTRATO` | Política moderna y parámetro `dblTNominal` | Aprobada MVP; límites pendientes |
| `LCR_FG_STATUS` | `1` | Constante de línea activa | Política moderna | Aprobada MVP, asignación Legacy pendiente |
| `LCR_FE_ULTMOD` | Fecha única de `CFECHA_OPERACION` | Fecha operativa de la transacción | Decisión moderna; reemplaza el `Now` observado | Aprobada MVP |
| `TLC_FL_CVE` | `2` | Resolución `CLTOPERACION` para `CD`; validar `CLINEA_CREDITO` activa | Evidencia agregada: `LINEA CD`, activa, 180 días | Confirmada para CD con validación previa |

`PNA_FL_PERSONA` no forma parte de las 18 columnas físicas `NOT NULL` de esta
matriz, pero es obligatorio por regla de aplicación, debe corresponder al
cliente seleccionado y se valida antes de reservar el consecutivo.

### Clasificación de la evidencia

| Categoría | Columnas/decisiones |
|---|---|
| Evidencia Legacy | `LCR_FL_CVE`, recepción de parámetros de fecha, moneda, montos, tasas, plazo y usuario en `ActualizaLineaUsuario` |
| Evidencia agregada | coincidencias de seis meses; relación `CD -> TLC_FL_CVE = 2`; catálogo activo de línea y configuración actual de CTASA |
| Decisión moderna aprobada | fecha operativa única, vencimiento de seis meses, línea activa `1`, moneda `1`, tasa fija, montos iniciales y no uso de valores históricos como defaults |
| Validación aún necesaria | catálogo/rango de plazo, catálogo de `LCR_CL_TLINEA`, límites de tasa nominal, propagación efectiva a KCONTRATO y transacción compartida |

Con estas decisiones, ninguna de las 18 columnas queda bloqueada por falta de
una decisión funcional del MVP. Permanecen validaciones técnicas y de código,
pero la línea automática ya está lista para diseño técnico restringido a CD;
el POST no se implementa hasta cerrar esas validaciones.

Resumen solicitado de estados: `LCR_FL_CVE` es la única columna con valor
directamente confirmado por la evidencia de generación Legacy; las otras 17
quedan cubiertas por decisiones modernas aprobadas para este MVP y por la
asignación de parámetros ya localizada, pero requieren verificación del
caller/adaptador antes de codificar. En términos funcionales del MVP hay 18
aprobadas, 0 parciales y 0 bloqueadas; en términos de trazabilidad técnica
hay 17 pendientes de verificación de integración. `LCR_FE_VENCIMIENTO` ya no
es un bloqueo: su fórmula aprobada es `DATEADD(MONTH, 6, fechaOperativa)`.

## `KLTOPERA` dentro del MVP

La fila se inserta en la misma transacción moderna con:

| Columna | Valor |
|---|---|
| `LCR_FL_CVE` | Identificador recién generado de la línea |
| `TOP_CL_CVE` | `CD` |
| `LTP_FE_ULTMOD` | La fecha operativa única de `CFECHA_OPERACION` |
| `USR_CL_CVE` | `LegacyUserCode` de la membresía activa seleccionada |

No se incrementa un consecutivo independiente para `KLTOPERA`. Cualquier
error en la línea, relación de operación, contrato, pagos, amortización,
CAT, cargos o bitácora revierte la transacción completa, incluido el
consecutivo de `KLINEA_CREDITO`.

## Bloqueos restantes de `KCONTRATO` para CD

El cierre de la línea no resuelve automáticamente las columnas obligatorias
del contrato. Las decisiones pendientes se mantienen separadas:

### Capturados por UI

- operación `CD` sólo si el catálogo activo lo permite;
- cliente/persona;
- plazo autorizado;
- monto a financiar;
- tasa nominal anual;
- datos financieros restantes únicamente cuando sus reglas CD estén cerradas.

### Catálogos y resoluciones server-side

- empresa/despliegue autorizado;
- tasa fija ordinaria en pesos de `CTASA`;
- `CNB` activo compatible con la operación;
- calendario, periodicidad, esquema de pago, exigibilidad y amortización;
- Uso CFDI compatible con el régimen fiscal;
- valores de estado de equipo y demás catálogos requeridos.

### Constantes del MVP

- sin reestructura, subsidio, propuesta, contrato maestro, PBA, RUSH,
  seguros financiados, multimóneda ni revisión;
- moneda nacional;
- línea nueva por contrato;
- tasa base, puntos adicionales y factor de línea en cero;
- línea activa y mínimo de plazo `1`.

### Derivados y cálculos posteriores

- clave `CTO_FL_CVE` mediante `spLsnetGeneraClaveContrato`;
- fechas desde la fecha operativa única;
- saldo inicial y montos financieros;
- fechas y pagos de amortización;
- cargos, CAT y bitácora obligatoria.

Continúan bloqueando el POST CD las columnas de `KCONTRATO` cuyo catálogo,
valor técnico, cálculo financiero o efecto posterior aún no tengan evidencia
Legacy completa. En particular: estado inicial, tasa nominal y campos
condicionales de seguros/pagos finales, CNBV, periodicidad/amortización,
fechas de desembolso/firma y la frontera exacta de procedimientos posteriores.

## Contrato HTTP propuesto para el futuro POST CD

El navegador enviará sólo datos funcionales, nunca identificadores técnicos
de línea, tasa, banco o consecutivos:

```json
{
  "personId": 0,
  "operationCode": "CD",
  "term": 0,
  "financedAmount": 0,
  "nominalAnnualRate": 0,
  "taxRegimeUseCode": "",
  "equipmentStatusCode": 0,
  "paymentPeriodicityCode": 0,
  "scheduleCode": 0,
  "dueRuleCode": 0,
  "calendarCode": 0,
  "amortizationCode": 0,
  "requestedDisbursementDate": ""
}
```

El ejemplo es de diseño, no un contrato implementado. El backend resolverá
`TAS_FL_CVE`, `TLC_FL_CVE`, empresa, CNBV y demás claves; validará readiness,
cliente, operación, catálogos y fecha operativa antes de reservar
consecutivos. La idempotencia se comprobará antes de reservar y nuevamente
bajo bloqueo dentro de la transacción para impedir duplicar línea,
`KLTOPERA`, contrato, pagos o cargos.

## Cierre de bloqueos de KCONTRATO para el MVP CD

Esta sección supersede los conteos históricos anteriores de este documento.
La evidencia directa del método `sd_clsContratoPropuesta.ActualizaContrato`
confirma que, en alta nueva, la sentencia recibe los argumentos de
`KCONTRATO` en el orden de la matriz y que la línea automática se invoca antes
del alta cuando `blnLinAut` es verdadero. La clasificación siguiente separa
esa evidencia de las decisiones modernas aprobadas.

### Fórmulas y asignaciones confirmadas en `ActualizaContrato`

En el `INSERT` Legacy se observan estas asignaciones:

- `CTO_NO_MTO_FINANCIAR = dblMtoFinanciar` y `CTO_NO_SALDO = dblMtoFinanciar`.
- `CTO_NO_MTO_ANTICIPO` y `CTO_NO_PRC_ANTICIPO` reciben `dblMtoAnt` y
  `dblPtjAnt` sólo cuando `ESQ_FG_ANT_RENTA = 1`; de lo contrario reciben 0.
- `CTO_NO_MTO_ENGANCHE` y `CTO_NO_PRC_ENGANCHE` reciben los mismos valores
  sólo cuando `ESQ_FG_ENGANCHE = 1`; de lo contrario reciben 0.
- `CTO_NO_DEPRENTAS = dblNoRent`, `CTO_NO_MTO_DEPRENTAS = dblMtoRent`,
  `CTO_CL_IVA = dblTasaIVA` y `CTO_NO_MTO_DEPOSITO = dblDeposito`.
- `CTO_NO_TASA_BASE`, `CTO_NO_PUNTOS_ADIC`, `CTO_NO_FACTOR` y
  `CTO_NO_TASA_NOMINAL` reciben los parámetros de tasa; para el MVP se
  validan contra la política de tasa fija y la nominal capturada.
- `CTO_CL_FPAGO` se escribe como literal `1` en la rama observada y
  `PPG_FL_CVE` recibe `intPeriodicidad`; su significado funcional CD todavía
  requiere cerrar los catálogos y no se generaliza a otras operaciones.
- `CTO_NO_MTO_VRESIDUAL`, `CTO_NO_PRC_VRESIDUAL`, `CTO_NO_MTO_PAGOFINAL` y
  `CTO_NO_PRC_PAGOFINAL` reciben cero si el esquema no habilita residual o
  pago final. En el MVP esas funciones están fuera de alcance.
- `CTO_NO_SALDO = dblSaldo`, donde la rutina inicializa `dblSaldo` con
  `dblMtoFinanciar`.
- `CTO_FG_TASA_REGULADA` controla que techo y piso sean los valores recibidos
  o cero; el MVP usa tasa no regulada y ambos ceros.
- `CTO_FG_OPC` y sus importes de opción de compra reciben los parámetros de
  pagos finales; el MVP no habilita la opción y usa cero.
- `CTO_FG_CHKLIST = 0`, `CTO_FG_CV = 0`, `CTO_NO_SDOANT = 0`,
  `CTO_FG_TASA_USAORDINARIA = 1` y `SCB_FL_CVE = 1` aparecen como valores
  técnicos en la sentencia de alta. `SCB_FL_CVE` aún requiere catálogo y
  significado funcional antes de declararse seguro para el MVP.
- `CTO_FG_REESTRUCTURA = 0`, `CTO_FG_CESION = intFgCesionado` y
  `CTO_FG_MAESTRO = intEsContMtro`; reestructura, cesión y maestro están
  excluidos del MVP y deben recibir la constante moderna aprobada sólo cuando
  se cierre su semántica física.

La fuente Legacy observada usa `Now` para varias fechas y `GETDATE()` para
`CTO_FE_OPTERMINA`. La decisión moderna de este documento reemplaza esas
fechas por la fecha operativa única cuando la columna forma parte del MVP; no
se copia el reloj Legacy.

### Periodicidad, calendario, amortización y pago

Los parámetros Legacy son `intPeriodicidad`, `intCalendario`,
`intExigibilidad`, `intEsqPago`, `intCveAmor` e `intTipoCalc`. El código
confirma el destino, pero no una fila única de catálogo para CD. Permanecen
pendientes de lectura y compatibilidad:

`PPG_FL_CVE`, `CTO_CL_CALENDARIO`, `CTO_CL_EXIGIBILIDAD`, `CTO_CL_ESQPAGO`,
`CTO_CL_AMORT` y `TLO_FL_CVE`.

No se usarán frecuencias históricas como regla. Si la configuración CD no
devuelve una combinación única y activa, el futuro POST fallará antes de
reservar consecutivos.

### Tasa moratoria

`ActualizaContrato` recibe `intTasaMora`, `intTipoCalcMora`,
`dblTasaBaseMora`, `dblPuntosMora`, `dblFactorMora` y `dblTasaNomMora`, pero
la evidencia disponible no cierra todavía la consulta exacta a `CTMORATORIO`
para `CD`, moneda `1` y configuración activa. El backend deberá resolverla
server-side y validar vigencia, compatibilidad y unicidad. El frontend no
enviará identificadores técnicos de tasa moratoria. Si falta la configuración,
la creación debe rechazarse controladamente antes de cualquier escritura.

### CNBV, CFDI y domicilio

- `CNB_FL_CVE` se recibe seleccionado, pero se valida contra `CCNB` activa y
  compatible con `CD`; `CTO_CL_EDOEQUIPO` se deriva de esa configuración.
- `UCO_CL_CLAVE` se recibe como clave funcional y se valida contra el régimen
  fiscal, `KRELACION_REGIMEN_USOCFDI` activa y `CUSO_COMPROBANTES` activo.
- `DMO_FL_CVE` debe pertenecer al cliente y estar activo. La decisión restante
  es si el alta exige predeterminado, dirección fiscal o uso de facturación;
  no se elige una de esas reglas por frecuencia.

### Empresa, sucursal y seguros

`EMP_FL_CVE = 1` queda aprobado para Toyota y se resuelve por despliegue, no
desde el navegador. `SUC_FL_CVE` y `SCB_FL_CVE` no se aceptan desde el
request. La sucursal, la plaza operativa y el catálogo de seguro aún deben
resolverse por configuración del tenant. `SCB_FL_CVE = 1` es un literal
observado, no una regla funcional cerrada.

### Estados y fechas restantes

La matriz separa fecha operativa, capturada y calculada:

| Campo | Estado actual |
|---|---|
| `CTO_FE_GENERACION`, `CTO_FE_ULTMOD` | Fecha operativa única de `CFECHA_OPERACION` |
| `CTO_FE_INICIO` | Capturada; validar contra operación y fecha operativa |
| `CTO_FE_PRIMER_PAGO` | Capturada o calculada según periodicidad; regla CD pendiente |
| `CTO_FE_ULTPAGO` | Calculada por pagos/amortización; regla CD pendiente |
| `CTO_FE_SOL_DESEMBOLSO` | Origen UI vs fecha operativa pendiente |
| `CTO_FE_ACTIVACION` | No se activa un contrato registrado sin transición aprobada |
| `CTO_FE_BAJA`, `CTO_FE_FIRMA_CONTRATO`, `CTO_FE_FIRMAANEXO` | No aplicables al registro inicial salvo regla Legacy confirmada; requieren valor técnico físico |
| `CTO_FE_ULTCALC_INTERES` | Posterior al cálculo; proceso y momento pendientes |
| `CTO_FG_STATUS` | `intStatCont` llega de la captura, pero el valor inicial registrado no está confirmado |

No se debe convertir una fecha no aplicable en `Now` ni `GETDATE`.

### Procesos posteriores y transacción

El código observado llama o puede llamar, según propuesta, reestructura y
configuración, a procesos de pagos, amortización, cargos, CAT y cálculo de
tabla. Para el MVP sin propuesta, la secuencia moderna propuesta es:

1. Validar idempotencia, cliente, readiness, domicilio, catálogos, fecha
   operativa, tasa, moratorio, CNBV, CFDI y parámetros financieros.
2. Reservar `LCR_FL_CVE` y crear `KLINEA_CREDITO`.
3. Crear `KLTOPERA` para `CD`.
4. Generar `CTO_FL_CVE` mediante `spLsnetGeneraClaveContrato`.
5. Insertar `KCONTRATO`.
6. Crear el cálculo de amortización y pagos requeridos por la configuración
   CD; actualizar CAT, cargos obligatorios y bitácora dentro de la misma
   transacción.
7. Confirmar sólo al finalizar todos los pasos.

Una falla en cualquiera de esos pasos revierte línea, operación, contrato,
consecutivos, pagos, cargos, CAT y bitácora. La evidencia Legacy de fronteras
separadas no cambia la obligación transaccional moderna.

### Matriz de cierre actual

El esquema físico continúa teniendo 70 columnas `NOT NULL`. Con las
decisiones aprobadas para el MVP y la asignación Legacy localizada:

- **Cerradas:** 47 columnas, incluyendo generación de clave, empresa,
  persona, línea, moneda, tasa fija, componentes excluidos, montos iniciales
  aprobados, domicilio y fechas operativas.
- **Parciales:** 0 como decisión funcional del MVP; las validaciones de
  catálogo y adaptador siguen siendo necesarias antes de escribir.
- **Bloqueadas:** 23 columnas, porque aún falta una regla o catálogo de CD:

  `CTO_FG_STATUS`, `CTO_FE_PRIMER_PAGO`, `CTO_FE_ULTPAGO`,
  `CTO_FE_SOL_DESEMBOLSO`, `CTO_FE_ACTIVACION`, `CTO_FE_BAJA`,
  `CTO_FE_FIRMA_CONTRATO`, `CTO_FE_FIRMAANEXO`, `CTO_FE_ULTCALC_INTERES`,
  `CTO_CL_EXIGIBILIDAD`, `CTO_CL_CALENDARIO`, `CTO_CL_FPAGO`, `PPG_FL_CVE`,
  `CTO_CL_ESQPAGO`, `CTO_CL_AMORT`, `TLO_FL_CVE`, `CTO_CL_FPAGO_SEGBIEN`,
  `TAS_FL_CVEMORA`, `TLO_FL_CVEMORA`, `TAS_NO_BASEMORA`,
  `CTO_NO_PUNTOS_MORA`, `CTO_NO_FACTOR_MORA`, `CTO_NO_NOMINAL_MORA`.

La cuenta 47/23 es el estado del MVP documental y no autoriza a ignorar una
columna física: cada bloqueada debe resolverse con catálogo, fecha no
aplicable, estado inicial o procedimiento posterior antes del POST.

## DTO mínimo propuesto, sin implementación

El request no debe aceptar identificadores técnicos que el backend puede
resolver. Para el MVP, el contrato propuesto es:

| Propiedad | Tipo .NET | Requerido | Validación | Destino |
|---|---|---:|---|---|
| `personId` | `int` | Sí | Cliente activo y alcance del tenant | `PNA_FL_PERSONA` |
| `operationCode` | `string` | Sí | Exactamente `CD`, operación activa | `TOP_CL_CVE` |
| `capital` | `decimal` | Sí | `numeric`/escala física y límites financieros | `CTO_NO_CAPITAL` |
| `downPaymentAmount` | `decimal` | Sí | No negativo; compatibilidad con esquema | `dblMtoAnt` y derivados de anticipo/enganche |
| `term` | `int` | Sí | Plazo permitido por CD | `CTO_NO_PLAZO`, línea |
| `startDate` | `DateOnly` | Sí | Fecha válida y compatible con fecha operativa | `CTO_FE_INICIO` |
| `firstPaymentDate` | `DateOnly` | Pendiente | Sólo si la regla CD no la deriva | `CTO_FE_PRIMER_PAGO` |
| `disbursementRequestDate` | `DateOnly` | Pendiente | Sólo si la operación lo exige | `CTO_FE_SOL_DESEMBOLSO` |
| `nominalAnnualRate` | `decimal` | Sí | `numeric(7,4)` y límites Legacy | `CTO_NO_TASA_NOMINAL`, línea |
| `cnbvCode` | `int` | Sí | `CCNB` activa y compatible con CD | `CNB_FL_CVE` |
| `cfdiUseCode` | `string` | Sí | Régimen, relación activa y catálogo activo | `UCO_CL_CLAVE` |
| `addressId` | `int` | Sí | Domicilio del cliente, activo y regla funcional pendiente | `DMO_FL_CVE` |
| `idempotencyKey` | `string` | Sí | Longitud/alfabeto controlados; única por actor y operación | Control de idempotencia |

`vatRate`, `paymentPeriodicityCode`, `EMP_FL_CVE`, `LCR_FL_CVE`, `CTO_FL_CVE`,
`TAS_FL_CVE`, `TLC_FL_CVE`, `SCB_FL_CVE`, estados, saldos, fechas de auditoría,
usuario Legacy, montos derivados y claves de moratorio quedan fuera del
request hasta cerrar si son derivados o catálogos obligatorios. Si la
periodicidad no puede resolverse por configuración CD, se agrega como
propiedad funcional, no como identificador técnico arbitrario.

## Resultado de preparación

A. **UI:** cliente, operación CD, capital, anticipo, plazo, fecha de inicio,
tasa nominal, CNBV, CFDI, domicilio y clave de idempotencia; fechas de primer
pago/desembolso sólo cuando se confirme que son capturadas.

B. **Catálogos:** operación, tasa fija, línea, CNBV, CFDI, domicilio,
periodicidad/amortización, moratorio y cualquier configuración de seguro
obligatoria.

C. **Derivados:** clave de contrato, línea, cartera, moneda, fechas
operativas, montos iniciales, saldo, tasas técnicas y pagos.

D. **Constantes MVP:** empresa `1`, operación `CD`, moneda `1`, línea nueva,
`TLC_FL_CVE = 2`, línea activa, tasa fija, sin multimóneda, sin subsidio,
residual, opción, pago final especial, propuesta o reestructura.

E. **Procesos transaccionales:** fecha operativa, consecutivo de línea,
`KLINEA_CREDITO`, `KLTOPERA`, SP de clave, `KCONTRATO`, pagos, amortización,
CAT, cargos y bitácora.

F. **Bloqueos restantes:** las 23 columnas listadas, especialmente reglas de
estado inicial, periodicidad/amortización, fechas de pagos/desembolso,
moratorio, seguro y procesos posteriores. Por tanto, catálogo/UI y el
servicio de cálculo/validación pueden diseñarse sin escritura, pero el POST
transaccional todavía no es seguro.

## Revisión adicional de estado, pagos y moratorio

No se recibió en el contexto de esta revisión un bloque nuevo de resultados
SQL con filas concretas de `CPERPAGO`, `CPARAMETRO`, `CTCALCULO` o
`CTMORATORIO`. En consecuencia, se incorporan únicamente las asignaciones que
sí están visibles en `ActualizaContrato` y se mantienen bloqueadas las
decisiones que requieren esos valores. Las distribuciones históricas no se
usan como defaults.

### Estado y fechas

`ActualizaContrato` recibe `intStatCont`, `strFecIni`, `strFecPrimerPago`,
`strFecSolDesem`, `strFecActivacion`, `strFecBaja`, `strFecFirmaCont`,
`strFecFirmaAnexo`, `strFecOper` y el usuario. La sentencia también asigna
fechas con el reloj Legacy (`Now`/`GETDATE`) en algunas rutas. Para uCredit
queda vigente la decisión moderna de usar una sola fecha de
`CFECHA_OPERACION`; no se copia el reloj Legacy.

| Columna | Origen visible | Estado |
|---|---|---|
| `CTO_FG_STATUS` | `intStatCont` | Bloqueada: falta valor inicial CD y transición autorizada |
| `CTO_FE_GENERACION` | `strFecOper`/fecha operativa | Confirmada por decisión moderna |
| `CTO_FE_INICIO` | `strFecIni` | Capturada; validar contra fecha operativa |
| `CTO_FE_PRIMER_PAGO` | `strFecPrimerPago` | Bloqueada: falta regla de cálculo/captura CD |
| `CTO_FE_ULTPAGO` | `Now` en ruta observada | Bloqueada: debe derivarse de pagos, no del reloj |
| `CTO_FE_SOL_DESEMBOLSO` | `strFecSolDesem` | Bloqueada: falta regla de aplicabilidad |
| `CTO_FE_ACTIVACION` | `strFecActivacion` | Bloqueada: falta transición de estado |
| `CTO_FE_BAJA` | `strFecBaja` | Bloqueada: falta centinela aprobado/no aplicable |
| `CTO_FE_FIRMA_CONTRATO` | `strFecFirmaCont` | Bloqueada: falta regla para alta registrada |
| `CTO_FE_FIRMAANEXO` | `strFecFirmaAnexo` | Bloqueada: falta regla para alta registrada |
| `CTO_FE_ULTCALC_INTERES` | proceso de cálculo | Bloqueada: falta momento y valor inicial |
| `CTO_FE_ULTMOD` | `Now` en ruta Legacy | Aprobada modernamente como `CFECHA_OPERACION`; requiere adaptar la escritura |

### Pagos y calendario

El código confirma que `PPG_FL_CVE` recibe `intPeriodicidad`,
`CTO_CL_EXIGIBILIDAD` recibe `intExigibilidad`, `CTO_CL_CALENDARIO` recibe
`intCalendario`, `CTO_CL_FPAGO` usa el literal Legacy `1`, `CTO_CL_ESQPAGO`
recibe `intEsqPago`, `CTO_CL_AMORT` recibe `intCveAmor` y `TLO_FL_CVE`
recibe `intTipoCalc`. No confirma por sí mismo las filas activas ni la
compatibilidad de CD.

Por tanto, siguen bloqueados `PPG_FL_CVE`, `CTO_CL_EXIGIBILIDAD`,
`CTO_CL_CALENDARIO`, `CTO_CL_ESQPAGO`, `CTO_CL_AMORT`, `TLO_FL_CVE` y
`CTO_CL_FPAGO_SEGBIEN`. Falta evidencia de:

- fila activa y valor exacto de `CPERPAGO` para la periodicidad CD;
- catálogo y valor de exigibilidad/calendario;
- significado funcional de `CTO_CL_FPAGO = 1` para este MVP;
- esquema de pago, tipo de amortización y tipo de cálculo compatibles;
- regla de seguro para `CTO_CL_FPAGO_SEGBIEN` aun cuando los seguros
  financiados estén fuera del MVP.

El futuro backend debe resolver estos valores server-side y rechazar una
configuración inexistente o ambigua antes de reservar consecutivos.

### Tasa moratoria

La correspondencia de parámetros es:

| `CTMORATORIO`/argumento | `KCONTRATO` |
|---|---|
| tipo de tasa moratoria / `intTasaMora` | `TAS_FL_CVEMORA` |
| tipo de cálculo / `intTipoCalcMora` | `TLO_FL_CVEMORA` |
| tasa base / `dblTasaBaseMora` | `TAS_NO_BASEMORA` |
| puntos / `dblPuntosMora` | `CTO_NO_PUNTOS_MORA` |
| factor / `dblFactorMora` | `CTO_NO_FACTOR_MORA` |
| tasa nominal / `dblTasaNomMora` | `CTO_NO_NOMINAL_MORA` |

La fuente Legacy muestra que esos valores llegan como argumentos, pero no se
localizó todavía una consulta inequívoca que resuelva una única configuración
activa por `CD` y moneda `1`. El frontend no enviará identificadores técnicos.
El backend deberá resolver `CTMORATORIO`, comprobar vigencia, moneda,
operación y unicidad, y responder `422 contract_late_rate_configuration_required`
si falta la configuración.

Siguen bloqueadas las seis columnas moratorias: `TAS_FL_CVEMORA`,
`TLO_FL_CVEMORA`, `TAS_NO_BASEMORA`, `CTO_NO_PUNTOS_MORA`,
`CTO_NO_FACTOR_MORA` y `CTO_NO_NOMINAL_MORA`.

### Recuento actualizado

Con la evidencia disponible en esta revisión:

- 70 columnas físicas `NOT NULL`.
- 47 cerradas por evidencia o decisión MVP previa.
- 0 parciales funcionales.
- 23 bloqueadas; son las mismas 23 listadas en la sección de cierre anterior,
  porque no se recibió evidencia SQL nueva con los valores exactos de pagos,
  estado, fechas y moratorio.

La evidencia faltante por grupo es: valor inicial de `CTO_FG_STATUS`, reglas
de fechas y centinelas, filas activas de `CPERPAGO`/catálogos de pago, regla
CD para periodicidad y amortización, catálogo de seguro aplicable y la fila
única activa de `CTMORATORIO` con su correspondencia a tasa y cálculo.

## Corrección de evidencia moratoria

Esta sección supersede el conteo y el estado de la sección moratoria anterior.

La evidencia SQL confirma una configuración para `CD` y moneda nacional:

| Campo | Valor observado |
|---|---:|
| `TMR_FL_CVE` | `7` |
| `TOP_CL_CVE` | `CD` |
| `TLO_FL_CVE` | `1` |
| `TAS_FL_CVE` | `1` |
| `TMR_CL_MONEDA` | `1` |
| `TMR_NO_FACTOR` | `0.0000` |
| `TMR_NO_PUNTOS` | `33.9000` |
| `TMR_NO_VALTASA` | `33.9000` |

El INSERT de alta de `ActualizaContrato` confirma la asignación de los
argumentos Legacy a KCONTRATO (`sd_clsContratoPropuesta.vb`, rama de alta):

| Argumento Legacy | Columna KCONTRATO | Estado de origen |
|---|---|---|
| `intTasaMora` | `TAS_FL_CVEMORA` | Confirmado: `CTMORATORIO.TAS_FL_CVE` |
| `intTipoCalcMora` | `TLO_FL_CVEMORA` | Confirmado: `CTMORATORIO.TLO_FL_CVE` |
| `dblTasaBaseMora` | `TAS_NO_BASEMORA` | Pendiente de asignación upstream inequívoca |
| `dblPuntosMora` | `CTO_NO_PUNTOS_MORA` | Confirmado: `CTMORATORIO.TMR_NO_PUNTOS` |
| `dblFactorMora` | `CTO_NO_FACTOR_MORA` | Confirmado: `CTMORATORIO.TMR_NO_FACTOR` |
| `dblTasaNomMora` | `CTO_NO_NOMINAL_MORA` | Pendiente de asignación upstream inequívoca |

`sd_clsParametrizacion.vb`, método `ObtenTasaMoratoria`, expone
`TMR_NO_VALTASA`, `TMR_NO_PUNTOS` y `TMR_NO_FACTOR`. En su variante de
resolución de tasa también expone `TSV_NO_VALOR AS TBASE` desde
`KTASA_VALOR`. Esto no demuestra que `TMR_NO_VALTASA` se copie directamente a
`TAS_NO_BASEMORA` ni a `CTO_NO_NOMINAL_MORA`; no se asignará así por inferencia.
El caller que alimenta `dblTasaBaseMora` y `dblTasaNomMora` antes de
`ActualizaContrato` debe quedar identificado antes del POST.

La consulta sin filas para `KCONTRATO` con `CTO_FG_STATUS = 1` no implica
ausencia de configuración moratoria. La resolución debe validar la fila de
`CTMORATORIO` por operación, moneda, tasa y tipo de cálculo.

En consecuencia, cuatro de las seis columnas moratorias están confirmadas y
permanecen bloqueadas únicamente:

- `TAS_NO_BASEMORA`: falta confirmar si proviene de `TSV_NO_VALOR`,
  `TMR_NO_VALTASA` u otra regla de alta.
- `CTO_NO_NOMINAL_MORA`: falta confirmar si se calcula con la tasa base,
  puntos/factor, `TMR_NO_VALTASA` u otra regla de alta.

### Recuento corregido

El recuento actualizado de las 70 columnas físicas `NOT NULL` es:

- **51 confirmadas** por evidencia o decisión MVP, incluyendo
  `TAS_FL_CVEMORA`, `TLO_FL_CVEMORA`, `CTO_NO_PUNTOS_MORA` y
  `CTO_NO_FACTOR_MORA`.
- **0 parciales funcionales**.
- **19 bloqueadas**: las 17 pendientes de estado, fechas, pagos y
  configuración, más `TAS_NO_BASEMORA` y `CTO_NO_NOMINAL_MORA`.

El futuro backend debe resolver la configuración moratoria server-side,
validar operación `CD`, moneda `1`, vigencia y unicidad, y responder
`422 contract_late_rate_configuration_required` si no existe una
configuración inequívoca o si no puede completar las dos asignaciones aún
pendientes.

## Evidencia adicional: alta nueva, pagos y moratorios

### Estado inicial

La distribución actual no contiene contratos `CD` con `CTO_FG_STATUS = 1`;
los estados observados son `3`, `5`, `6`, `10` y `11`. Esa distribución no
define el estado de alta. En `GuardaInfo`, la llamada de contrato nuevo a
`ActualizaContrato` pasa el literal `1` en la posición de `intStatCont`
(`su_actContratoPropuesta.aspx.vb`, bloque de guardado de contrato). Por
tanto, para el flujo de alta nueva queda confirmada la secuencia: crear con
`CTO_FG_STATUS = 1` y permitir que procesos posteriores lo cambien. No se
deduce esa regla de la distribución histórica.

### Pagos: cadena confirmada y valores pendientes

En `su_actContratoPropuesta.aspx.vb`, durante la carga de la pantalla se
llenan los controles y, al guardar, se copian sus valores a los argumentos de
`ActualizaContrato`:

| Columna KCONTRATO | Control/argumento | Catálogo o fuente Legacy | Estado para CD |
|---|---|---|---|
| `CTO_CL_EXIGIBILIDAD` | `cmbExigibilidad` → `intPCveExig` | `CPARAMETRO`, catálogo `34` | Valor exacto CD pendiente |
| `CTO_CL_CALENDARIO` | `cmbCalendGen` → `intPCveCalen` | `CPARAMETRO`, catálogo `8` | Valor exacto CD pendiente |
| `PPG_FL_CVE` | `cmbPeriodicidad` → `intPPeriod` | `CPERPAGO` mediante `ObtenPerPago` | Valor exacto CD pendiente |
| `CTO_CL_ESQPAGO` | `rdbEsquemas` → `intPEsqPago` | `CESQUEMA_CALCULO`, `CEC_CL_ESQPAGO` | Valor exacto CD pendiente |
| `CTO_CL_AMORT` | regla de `rdbAmort*` → `intPCveTipoAmort` | opciones de amortización de la pantalla | Valor exacto CD pendiente |
| `TLO_FL_CVE` | `cmbTipoCalc` → `intPCveTipoCal` | `CTCALCULO` mediante `ObtenTipoCalculoDs` | Debe resolver la configuración CD |
| `CTO_CL_FPAGO_SEGBIEN` | radios seguro → `intPCveFoPago` | opciones `1=contado`, `2=financiado`, `3=cuenta cliente` | No aplica sin seguro; centinela pendiente |

La pantalla establece el catálogo de exigibilidad con `LlenaComboParametros`
clave `34`, calendario con clave `8`, periodicidad con `ObtenPerPago`, y
esquema con `sn_clsEsquemaPago.ObtenEsquemaPago`, que consulta
`CESQUEMA_CALCULO`. El código de guardado no fija los valores predominantes
`1/3/1/3/1`; los toma de controles. Por ello la frecuencia observada sólo es
evidencia auxiliar y no cierra el MVP.

Las variantes confirmadas físicamente para `CTO_CL_FPAGO`, `TLO_FL_CVE` y
`CTO_CL_FPAGO_SEGBIEN` requieren una regla de operación CD y no pueden
seleccionarse por frecuencia. Esos valores deben resolverse server-side o
capturarse mediante catálogos autorizados, nunca como identificadores libres.

### Moratorios: configuración posterior frente a valores del alta

La configuración observada en `CTMORATORIO` para CD/pesos es:

| Campo | Valor |
|---|---:|
| `TMR_FL_CVE` | `7` |
| `TLO_FL_CVE` | `1` |
| `TAS_FL_CVE` | `1` |
| `TMR_NO_FACTOR` | `0` |
| `TMR_NO_PUNTOS` | `33.9` |
| `TMR_NO_VALTASA` | `33.9` |

El code-behind carga esa configuración mediante `ObtenTasaMoratoria` y
rellena `cmbTipoTasaTM`, `cmbTipoCalcTM`, `txtTasaBaseTM`, `txtPuntosTM` y
`txtFactorTM`. Al guardar, `GuardaInfo` vuelve a leer los controles y calcula
`lblTasaNominalTM`; después los envía a `ActualizaContrato`. Esto demuestra
que la pestaña moratoria participa en el alta Legacy, aunque sus valores
puedan reutilizar una configuración técnica posterior. No demuestra que
`33.9` deba persistirse como valor inicial del contrato.

El mapeo de la sentencia de `ActualizaContrato` queda así:

| Columna | Asignación en alta | Estado |
|---|---|---|
| `TAS_FL_CVEMORA` | `intTasaMora` / `cmbTipoTasaTM` | Referencia técnica; valor CD inicial debe validarse contra CTMORATORIO |
| `TLO_FL_CVEMORA` | `intTipoCalcMora` / `cmbTipoCalcTM` | El default observado es `1`; el control permite otra selección, por lo que falta cerrar la regla CD |
| `TAS_NO_BASEMORA` | `dblTasaBaseMora` / `txtTasaBaseTM` | Pendiente: el caller puede recibir `TSV_NO_VALOR`; no usar `TMR_NO_VALTASA` por inferencia |
| `CTO_NO_PUNTOS_MORA` | `dblPuntosMora` / `txtPuntosTM` | Configuración observada `33.9`, pero no aprobada como valor inicial del contrato |
| `CTO_NO_FACTOR_MORA` | `dblFactorMora` / `txtFactorTM` | Configuración observada `0`; no convertir frecuencia contractual en regla |
| `CTO_NO_NOMINAL_MORA` | `dblTasaNomMora` / `lblTasaNominalTM` | Calculada por `TasaNominal`; fórmula y valor inicial CD aún requieren cierre |
| `CTO_FG_TASA_USAORDINARIA` | `chkTasaOrdinariaBase` | La pantalla lo envía; para el MVP se propone `0`, pendiente de confirmación de alta |

La mayoría histórica de contratos con ceros no prueba por sí sola que el alta
cree ceros. La propuesta de MVP (`1,1,0,0,0,0,0`) queda documentada sólo como
propuesta, no como regla aprobada, hasta localizar la asignación de alta que
la confirme. La primera versión de UI no debe exponer una pestaña moratoria:
el backend debe resolver su configuración y rechazarla de forma controlada
si es ambigua (`422 contract_late_rate_configuration_required`).

### Recuento documental revisado

- **51 columnas cerradas** por evidencia o decisión MVP previa, incluyendo el
  estado inicial `CTO_FG_STATUS = 1`.
- **19 columnas bloqueadas**: pagos con variante no resuelta, fechas y
  centinelas pendientes, configuración de seguro y asignaciones moratorias
  cuyo valor de alta no está confirmado.
- No se marcan como reglas los valores `1/3/1/3/1`, `33.9` ni los ceros
  históricos.

El POST todavía no puede diseñarse de forma segura. Faltan los valores CD
exactos y vigentes de los catálogos de pagos, la regla de no-aplicabilidad del
seguro, la selección definitiva de `TLO_FL_CVE` y la asignación inicial de
base/nominal moratorios. Los campos de selector serán exigibilidad,
calendario, periodicidad, esquema de pago, amortización, tipo de cálculo y
CNBV; el backend derivará empresa, línea, moneda, fechas operativas, tasas
técnicas y referencias moratorias.

## Cierre técnico de las columnas pendientes del MVP CD

Esta sección supersede los recuentos anteriores cuando difieren. La fuente
primaria es el flujo de alta nueva de
`Migrado/su_actContratoPropuesta.aspx.vb`, su llamada a `ActualizaContrato` y
la construcción del `INSERT` en `sdLsenet/sd_clsContratoPropuesta.vb`.
Las frecuencias históricas sólo se usan como contraste.

### Trazabilidad de pagos y fechas

En alta nueva, `GuardaInfo` obtiene los valores de pantalla y los pasa a
`ActualizaContrato`: `cmbExigibilidad` a `intPCveExig`, `cmbCalendGen` a
`intPCveCalen`, `cmbPeriodicidad` a `intPPeriod`, `rdbEsquemas` a
`intPEsqPago`, los radios de amortización a `intPCveTipoAmort`,
`cmbTipoCalc` a `intPCveTipoCal` y los radios de seguro a
`intPCveFoPago`. La carga de controles confirma las fuentes, pero no una fila
única compatible con CD:

- exigibilidad: `LlenaComboParametros(..., 34, ...)`;
- calendario: `LlenaComboParametros(..., 8, ...)`;
- periodicidad: `ObtenPerPago` sobre `CPERPAGO`;
- esquema: `sn_clsEsquemaPago.ObtenEsquemaPago`, sobre
  `CESQUEMA_CALCULO`;
- tipo de cálculo: `ObtenTipoCalculoDs`, sobre `CTCALCULO`;
- seguro: radios de la pantalla, con valores Legacy `1=contado`,
  `2=financiado`, `3=cuenta cliente`.

El INSERT Legacy contiene además un literal `1` para `CTO_CL_FPAGO`. Ese
campo no pertenece a las 19 pendientes: queda cerrado como constante Legacy
para esta ruta. No se generaliza a otras operaciones.

Las fechas observadas en la validación y el INSERT son distintas de la
política moderna aprobada:

- `txtFechaIni` y `txtFechaPriPag` son controles de usuario; la pantalla exige
  fecha de inicio y primer pago, y si falta desembolso copia la fecha del
  primer pago a `txtFechaDesem`.
- `txtFechaDesem` se valida contra la fecha de inicio; no se localizó en este
  flujo una regla CD que lo derive directamente de `CFECHA_OPERACION`.
- activación, baja, firma de contrato y firma de anexo usan el valor de
  pantalla cuando existe; el code-behind asigna `1900-01-01` cuando están
  vacíos (`su_actContratoPropuesta.aspx.vb`, bloque de extracción de fechas).
- el INSERT Legacy usa `Now` para `CTO_FE_ULTPAGO` y `CTO_FE_ULTMOD`; la
  política moderna prohíbe esa fuente y exige una única `CFECHA_OPERACION`
  leída dentro de la transacción.
- `ActualizaFechaUltimoPago` puede cambiar posteriormente
  `CTO_FE_ULTPAGOORIGINAL` y `CTO_FE_ULTPAGO` con la fecha del pago; por ello
  la fecha inicial y la fecha posterior no deben confundirse.
- el INSERT posiciona `strFecOper` en el campo de último cálculo de interés
  del contrato; la asignación debe conservarse como `CFECHA_OPERACION` en el
  diseño moderno, no como reloj del servidor.

### Matriz exacta de las 19 columnas todavía bloqueadas

Se usa deliberadamente sólo `cerrada` o `bloqueada por evidencia faltante`.
Una fila queda bloqueada si el código muestra el parámetro pero no permite
demostrar la regla CD implementable y vigente.

| Columna | Origen exacto | Valor/regla MVP CD | Evidencia Legacy | Validación backend | Estado |
|---|---|---|---|---|---|
| `CTO_CL_EXIGIBILIDAD` | `cmbExigibilidad` → `intPCveExig` | Falta fila CD única de CPARAMETRO 34 | `su_actContratoPropuesta.aspx.vb`, carga y `GuardaInfo`; `ActualizaContrato` lo inserta | CPARAMETRO 34 activo y compatible con CD | bloqueada por evidencia faltante |
| `CTO_CL_CALENDARIO` | `cmbCalendGen` → `intPCveCalen` | Falta fila CD única de CPARAMETRO 8 | carga por `LlenaComboParametros`; argumento de `ActualizaContrato` | CPARAMETRO 8 activo y compatible con CD | bloqueada por evidencia faltante |
| `PPG_FL_CVE` | `cmbPeriodicidad` → `intPPeriod` | Falta periodicidad CD autoritativa | `ObtenPerPago(0,1,2,...)` y extracción de combo | `CPERPAGO` activo, compatible con moneda/operación y esquema | bloqueada por evidencia faltante |
| `CTO_CL_ESQPAGO` | `rdbEsquemas` → `intPEsqPago` | Falta esquema CD único | `ObtenEsquemaPago` consulta `CESQUEMA_CALCULO`; valor seleccionado se pasa a `ActualizaContrato` | clave activa y compatible con la operación | bloqueada por evidencia faltante |
| `CTO_CL_AMORT` | radios `rdbAmort*` → `intPCveTipoAmort` | Falta regla CD entre las opciones | `GuardaInfo` asigna 1/2/3 según radio; INSERT recibe `intCveAmor` | catálogo/regla de amortización activa para CD | bloqueada por evidencia faltante |
| `TLO_FL_CVE` | `cmbTipoCalc` → `intPCveTipoCal` | Falta tipo de cálculo ordinario CD | `ObtenTipoCalculoDs` y llamada a `ActualizaContrato` | `CTCALCULO` activo y compatible con tasa/esquema | bloqueada por evidencia faltante |
| `CTO_CL_FPAGO_SEGBIEN` | radios seguro → `intPCveFoPago` | Falta centinela confirmado cuando no aplica seguro | `GuardaInfo` usa 1/2/3; no hay regla CD de no-aplicabilidad | catálogo/regla de seguro; rechazar valor libre | bloqueada por evidencia faltante |
| `CTO_FE_PRIMER_PAGO` | `txtFechaPriPag` | Fecha capturada; debe cumplir regla CD de periodicidad | validación exige fecha y `ValidaFechaPrimerPago` | no anterior al inicio; fecha válida y compatible con calendario | bloqueada por evidencia faltante |
| `CTO_FE_ULTPAGO` | INSERT usa `Now`; después `ActualizaFechaUltimoPago` | Fecha inicial calculada por regla de pagos, no `Now` | `sd_clsContratoPropuesta.vb`, INSERT y `ActualizaFechaUltimoPago` | derivar desde pagos y fecha operativa; no reloj servidor | bloqueada por evidencia faltante |
| `CTO_FE_SOL_DESEMBOLSO` | `txtFechaDesem`, o copia de primer pago | Falta decidir captura/derivación CD | validación y argumento `strFecSolDesem` | fecha válida, no anterior al inicio; fuente única documentada | bloqueada por evidencia faltante |
| `CTO_FE_ACTIVACION` | `txtFechaAct` o `1900-01-01` | Centinela y transición inicial CD deben aprobarse | extracción de fechas en code-behind; INSERT recibe `strFecActivacion` | no activar sin evento autorizado; centinela sólo si contrato lo permite | bloqueada por evidencia faltante |
| `CTO_FE_BAJA` | `txtFechaBaja` o `1900-01-01` | Centinela inicial CD debe aprobarse | extracción de fechas; INSERT recibe `strFecBaja` | no baja en alta nueva; valor técnico compatible | bloqueada por evidencia faltante |
| `CTO_FE_FIRMA_CONTRATO` | `txtFechaFirCont` o `1900-01-01` | Centinela inicial CD debe aprobarse | code-behind y `strFecFirma` | no futura respecto a fecha operativa; aplicabilidad CD | bloqueada por evidencia faltante |
| `CTO_FE_FIRMAANEXO` | `txtFechaFirAnex` o `1900-01-01` | Centinela inicial CD debe aprobarse | code-behind y `strFecAnexo` | no futura; sólo si el anexo aplica | bloqueada por evidencia faltante |
| `CTO_FE_ULTCALC_INTERES` | `strFecOper` en el INSERT Legacy | `CFECHA_OPERACION` única del alta moderna | lista de columnas/valores de `ActualizaContrato`; Legacy lo ubica junto a flags de cálculo | exactamente una fecha operativa válida antes de reservar consecutivos | bloqueada por evidencia faltante |
| `TAS_NO_BASEMORA` | `dblTasaBaseMora` desde `txtTasaBaseTM`/configuración | No copiar automáticamente `TMR_NO_VALTASA` | `ObtenTasaMoratoria` muestra `TSV_NO_VALOR AS TBASE`; caller final aún no localizado | configuración CD/pesos única; error `422 contract_late_rate_configuration_required` | bloqueada por evidencia faltante |
| `CTO_NO_NOMINAL_MORA` | `dblTasaNomMora` desde `lblTasaNominalTM` | Fórmula/valor inicial de alta no confirmado | code-behind calcula/expone `TasaNominal`, pero falta expresión completa del caller | fórmula Legacy reproducida como regla explícita; no inferir de 33.9 | bloqueada por evidencia faltante |
| `CTO_FG_TASA_USAORDINARIA` | `chkTasaOrdinariaBase` → `bUsaTasaOrdinaria` | Falta regla CD: no asumir `0` histórico | `GuardaInfo` convierte booleano a 0/1 y lo pasa al INSERT | política de tasa fija CD y compatibilidad con tasa moratoria | bloqueada por evidencia faltante |

La lista anterior explica el aparente desfase de los recuentos previos: el
literal `CTO_CL_FPAGO = 1` y `CTO_FG_STATUS = 1` ya están cerrados por código,
mientras que `CTO_FG_TASA_USAORDINARIA` debe contarse explícitamente entre las
pendientes. Por tanto, el estado actual de las 70 columnas NOT NULL es:

- **51 cerradas** por evidencia Legacy o decisión MVP ya aprobada;
- **19 bloqueadas por evidencia faltante**;
- **0 parciales**.

Las 19 bloqueadas son exactamente las filas de la matriz anterior. El bloqueo
no se resolverá con frecuencias ni con el valor predeterminado de un control:
requiere localizar las filas activas y compatibles con CD, o documentar una
decisión funcional moderna explícita.

### Evaluación de seguridad del flujo

- **UI y catálogos GET:** sí pueden diseñarse e implementarse como lectura,
  siempre que devuelvan sólo catálogos activos y no permitan enviar
  identificadores técnicos no resueltos.
- **Servicio de cálculo sin escritura:** sí puede implementarse para validar
  persona, operación, moneda, fecha operativa, línea, tasas y fórmulas, pero
  debe devolver un estado bloqueado mientras falten los catálogos de pagos y
  las dos asignaciones moratorias.
- **POST `/api/v1/contracts`:** todavía no es seguro. Faltan reglas
  implementables para las 19 columnas, especialmente pagos, fechas no
  aplicables, base/nominal moratorios y `CTO_FG_TASA_USAORDINARIA`.
- **Siguiente unidad mínima:** completar consultas de sólo lectura y el
  servicio de resolución/validación de configuración CD, sin reservar
  consecutivos ni escribir KLINEA_CREDITO, KLTOPERACION o KCONTRATO.

### Evidencia Legacy revisada en esta etapa

- `Sitio Web/Migrado/su_actContratoPropuesta.aspx`;
- `Sitio Web/Migrado/su_actContratoPropuesta.aspx.vb`, incluidos Page_Load,
  carga de combos, validación de fechas y `GuardaInfo`;
- `sdLsenet/sd_clsContratoPropuesta.vb`, INSERT/UPDATE de KCONTRATO y
  `ActualizaFechaUltimoPago`;
- `sdLsenet/sd_clsParametrizacion.vb`, `ObtenTasaMoratoria`;
- `Proleasenet.EsquemaPago/sn_clsEsquemaPago.vb`, `ObtenEsquemaPago`;
- catálogos invocados por la pantalla: CPARAMETRO 34/8, CPERPAGO,
  CESQUEMA_CALCULO, CTCALCULO y CTMORATORIO.

La evidencia faltante queda delimitada por fila en la matriz; no se vuelve a
solicitar el esquema físico de KCONTRATO ni distribuciones ya recibidas.

## Auditoría final del conteo físico

El conteo se recalculó desde la matriz física de 70 columnas `NOT NULL`, sin
reutilizar los totales anteriores. Cada columna aparece exactamente una vez
en las listas siguientes.

### Columnas `NOT NULL` cerradas (51)

`CTO_FL_CVE`, `EMP_FL_CVE`, `TAS_FL_CVE`, `LCR_FL_CVE`,
`CTO_FE_GENERACION`, `CTO_FE_INICIO`, `CTO_FG_REESTRUCTURA`, `CTO_FG_STATUS`,
`CTO_CL_MONEDA`, `CTO_NO_MTO_FINANCIAR`, `CTO_NO_MTO_ANTICIPO`,
`CTO_NO_PRC_ANTICIPO`, `CTO_NO_PLAZO`, `CTO_NO_MTO_ENGANCHE`,
`CTO_NO_PRC_ENGANCHE`, `CTO_NO_DEPRENTAS`, `CTO_NO_MTO_DEPRENTAS`,
`CTO_CL_IVA`, `CTO_NO_MTO_DEPOSITO`, `CTO_NO_TASA_BASE`,
`CTO_NO_PUNTOS_ADIC`, `CTO_NO_FACTOR`, `CTO_NO_TASA_NOMINAL`,
`CTO_CL_FPAGO`, `CTO_FG_SEGVIDA`, `CTO_NO_MTO_VRESIDUAL`,
`CTO_NO_PRC_VRESIDUAL`, `CTO_NO_SALDO`, `CTO_FE_ULTMOD`,
`CTO_FG_TASA_REGULADA`, `CTO_NO_TASA_TECHO`, `CTO_NO_TASA_PISO`,
`TAS_FL_CVEMORA`, `TLO_FL_CVEMORA`, `CTO_NO_PUNTOS_MORA`,
`CTO_NO_FACTOR_MORA`, `CTO_NO_MTO_OPCIONCOMPRA`,
`CTO_NO_PRC_OPCIONCOMPRA`, `CTO_NO_MTO_PAGOFINAL`,
`CTO_NO_PRC_PAGOFINAL`, `CTO_FG_CHKLIST`, `CTO_FG_MAESTRO`,
`CTO_NO_ANEXO`, `CTO_NO_GRACIA_INT`, `CTO_FG_CESION`, `CTO_FG_CV`,
`PNA_FL_PERSONA`, `SCB_FL_CVE`, `CTO_NO_SDOANT`, `CTO_NO_EVM`,
`CTO_NO_PORC_EVM`.

Entre las cerradas se incluyen `CTO_FG_STATUS = 1` para alta nueva y
`CTO_CL_FPAGO = 1` por el literal visible en el INSERT Legacy. También se
incluyen las decisiones modernas ya aprobadas para tasa fija CD, moneda,
línea automática, montos iniciales, referencias moratorias y fechas
operativas. Esas decisiones son específicas del MVP CD, no defaults globales.

### Columnas `NOT NULL` bloqueadas (19)

| Columna | Motivo exacto del bloqueo |
|---|---|
| `CNB_FL_CVE` | Falta seleccionar una fila activa inequívoca de `CCNB` compatible con la operación CD; `CNB_FG_REGDEFAULT` sólo es selección inicial. |
| `CTO_CL_EDOEQUIPO` | No está cerrada la regla de estado de equipo para CD ni su valor de alta. |
| `CTO_CL_EXIGIBILIDAD` | Falta la fila activa CD de CPARAMETRO 34. |
| `CTO_CL_CALENDARIO` | Falta la fila activa CD de CPARAMETRO 8. |
| `PPG_FL_CVE` | Falta la periodicidad CD autoritativa de `CPERPAGO`. |
| `CTO_CL_ESQPAGO` | Falta el esquema activo compatible de `CESQUEMA_CALCULO`. |
| `CTO_CL_AMORT` | Falta la opción de amortización autorizada para CD. |
| `TLO_FL_CVE` | Falta el tipo de cálculo ordinario CD de `CTCALCULO`. |
| `CTO_CL_FPAGO_SEGBIEN` | Falta el centinela de no-aplicabilidad del seguro. |
| `CTO_FE_PRIMER_PAGO` | La pantalla captura la fecha, pero falta cerrar su regla CD con periodicidad y calendario. |
| `CTO_FE_ULTPAGO` | Legacy usa `Now` en el INSERT y luego puede actualizarla; falta la regla inicial moderna derivada de pagos. |
| `CTO_FE_SOL_DESEMBOLSO` | Falta decidir si CD la captura o la deriva; Legacy copia el primer pago si viene vacía. |
| `CTO_FE_ACTIVACION` | Falta aprobar aplicabilidad y valor inicial de la transición de activación. |
| `CTO_FE_BAJA` | Falta aprobar el valor inicial no aplicable y la transición de baja. |
| `CTO_FE_FIRMA_CONTRATO` | Falta aprobar aplicabilidad y centinela inicial para CD. |
| `CTO_FE_FIRMAANEXO` | Falta aprobar aplicabilidad y centinela inicial para CD. |
| `CTO_FE_ULTCALC_INTERES` | Aunque Legacy posiciona `strFecOper`, falta cerrar que el primer cálculo de interés ocurra en ese momento. |
| `TAS_NO_BASEMORA` | No se confirmó si el alta usa `TSV_NO_VALOR`, `TMR_NO_VALTASA` u otra fuente. |
| `CTO_NO_NOMINAL_MORA` | No se confirmó la expresión de `TasaNominal` y su valor inicial de alta. |

La suma es `51 + 19 = 70`. `CTO_FG_STATUS` y `CTO_CL_FPAGO` no regresaron
a bloqueadas: fueron retiradas de esa lista por evidencia directa. Tampoco se
incluye `CTO_FG_TASA_USAORDINARIA`, porque es nullable físicamente.

### Fechas con posible centinela

| Columna | Expresión Legacy | Condición | Regla MVP CD | Estado |
|---|---|---|---|---|
| `CTO_FE_ACTIVACION` | `If(txtFechaAct.Text.Trim.Length > 0, Format(CDate(txtFechaAct.Text), "yyyy-MM-dd"), "1900-01-01")` | Fecha capturada o control vacío | No activar durante el alta sin evento; definir valor técnico no aplicable | Requiere aprobación funcional |
| `CTO_FE_BAJA` | `If(txtFechaBaja.Text.Trim.Length > 0, Format(CDate(txtFechaBaja.Text), "yyyy-MM-dd"), "1900-01-01")` | Fecha capturada o control vacío | No dar de baja en alta nueva; definir valor técnico no aplicable | Requiere aprobación funcional |
| `CTO_FE_FIRMA_CONTRATO` | `If(txtFechaFirCont.Text.Trim.Length > 0, Format(CDate(txtFechaFirCont.Text), "yyyy-MM-dd"), "1900-01-01")` | Firma capturada o vacía | Sólo usar fecha real si CD exige firma al alta; no aprobar centinela global | Requiere aprobación funcional |
| `CTO_FE_FIRMAANEXO` | `If(txtFechaFirAnex.Text.Trim.Length > 0, Format(CDate(txtFechaFirAnex.Text), "yyyy-MM-dd"), "1900-01-01")` | Anexo capturado o vacío | Sólo usar fecha real si el anexo aplica; no aprobar centinela global | Requiere aprobación funcional |
| `CTO_FE_SOL_DESEMBOLSO` | `txtFechaDesem`; si está vacío, la pantalla asigna `txtFechaPriPag` | Desembolso vacío | Confirmar si CD usa primer pago o fecha operativa; no usar `Now` | Bloqueada |
| `CTO_FE_PRIMER_PAGO` | `txtFechaPriPag` validada por `ValidaFechaPrimerPago` | Captura obligatoria en la ruta de contrato | Fecha capturada y compatible con periodicidad/calendario | Bloqueada |
| `CTO_FE_ULTPAGO` | `Format(Now, "yyyyMMdd")` en INSERT; `ActualizaFechaUltimoPago` la modifica después | Alta y pagos posteriores | Derivar con la regla de pagos usando fecha operativa única | Bloqueada |
| `CTO_FE_ULTCALC_INTERES` | `strFecOper` en la lista de valores del INSERT | Alta Legacy | Usar `CFECHA_OPERACION`; confirmar el momento del primer cálculo | Bloqueada |

Ningún `1900-01-01` se aprueba como regla general. Las cuatro columnas que
usan ese centinela en el code-behind requieren decisión funcional individual.
Las fechas modernas no usarán `DateTime.Now`, `GETDATE` ni la fecha del equipo.

### Bloqueos funcionales adicionales

Estos elementos no pertenecen a las 70 columnas `NOT NULL`, pero pueden
impedir un POST seguro:

| Elemento | Naturaleza | Bloqueo |
|---|---|---|
| `CTO_FG_TASA_USAORDINARIA` | Nullable | Falta decidir si CD fija `0` o conserva una configuración de tasa ordinaria. |
| `DMO_FL_CVE` | Nullable | Falta cerrar si el contrato CD requiere domicilio activo y cuál se selecciona. |
| `UCO_CL_CLAVE` | Nullable | Requiere uso CFDI compatible con régimen, catálogo y relación activa. |
| `CTO_NO_TIR` | Nullable/proceso posterior | Falta confirmar cuándo se calcula y si pertenece al alta o a amortización. |
| `CTO_NO_PORC_CAT` | Nullable/proceso posterior | Falta confirmar cálculo CAT y su frontera transaccional. |
| `CTO_FE_ULTPAGOORIGINAL` | Nullable/proceso posterior | Se actualiza junto con pagos; requiere regla inicial y de actualización. |
| Amortización, pagos, CAT y cargos | Procesos posteriores | Deben ejecutarse dentro de la misma transacción moderna y tener rollback verificable. |

**Bloqueos funcionales adicionales: 7.** La columna `CTO_FG_TASA_USAORDINARIA`
no altera el conteo de las 70.

## Resumen auditado

- **NOT NULL cerradas: 51**.
- **NOT NULL bloqueadas: 19**.
- **Total físico NOT NULL: 70**.
- **Bloqueos funcionales adicionales nullable/procesos: 7**.
- **UI y catálogos GET implementables:** sí, como lectura y resolución sin
  escritura.
- **Cálculo sin escritura implementable:** sí, devolviendo bloqueo controlado
  cuando falte cualquiera de las configuraciones anteriores.
- **POST seguro:** no.

La corrección principal del conteo es que `CTO_FG_STATUS` y `CTO_CL_FPAGO`
quedan cerradas por evidencia Legacy; `CTO_FG_TASA_USAORDINARIA` se mueve a
los bloqueos funcionales adicionales por ser nullable y no se cuenta entre
las 70. No se mantuvo el total anterior por inercia: las dos listas son
exhaustivas y disjuntas.
