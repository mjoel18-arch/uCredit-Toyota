# Módulo Contracts — Consulta de contratos

## Objetivo

Consultar contratos con resultados equivalentes a ProLeaseNet, eliminando SQL concatenado y separando la consulta de alta/cancelación.

## Legacy de referencia

- `Migrado/su_catContratos.aspx`
- `Migrado/su_catContratos.aspx.vb`
- `snLsenet.sn_clsContratoPropuesta`
- `sdLsenet.sd_clsContratoPropuesta`
- `Proleasenet.Contratos.Contratos`

## Criterios

- contrato;
- clave de persona;
- RFC;
- nombre/razón social;
- VIN;
- solicitud;
- tipo de operación;
- estatus.

## Objetos SQL

`KCONTRATO`, `CMONEDA`, `KDESEMBOLSO`, `KTOPERACION`, `CPARAMETRO`, `CPERSONA`, `CEMPRESA`, `CUSUARIO`, `KCARAC_PROD_FACT`, `KPRODUCTO_FACTURA` y `CCATALOGO_CONTRATO`.

## Endpoint

```http
GET /api/v1/contracts
```

Parámetros: `contractNumber`, `personId`, `rfc`, `personName`, `vin`, `operationType`, `status`, `page`, `pageSize` y `sort`.

Exigir al menos un criterio principal, salvo permiso especial. `pageSize` debe estar limitado.

```http
GET /api/v1/contracts/{contractNumber}
```

Devuelve el detalle inicial de sólo lectura mediante `ContractDetailResponse`.

## Resultado resumido

- número de contrato;
- persona;
- tipo de operación;
- dirección;
- monto financiado;
- saldo insoluto;
- fechas de desembolso, primer y último pago;
- código/descripción de estatus;
- usuario y fecha de modificación.

## Permiso

`contracts.read`, con equivalencia inicial al control Legacy 56.

## Reglas

- SQL parametrizado.
- Paginación en servidor.
- Dinero como `decimal`.
- Fechas ISO 8601 en API.
- Búsqueda por contrato exacta.
- Coincidencia exacta de VIN y solicitud mediante `EXISTS`; los códigos de catálogo están pendientes de confirmación.
- Filtros de empresa/cartera aplicados por el servidor mediante `AllowedCompanyIds`.
- No incluir alta o cancelación.

## Aceptación

- mismos contratos que Legacy para casos acordados;
- mismos montos, saldos, fechas, operación y estatus;
- usuario sin permiso recibe 403;
- entrada de inyección no altera la consulta;
- paginación estable;
- no se modifica ninguna tabla;
- rendimiento objetivo aprobado con datos representativos.

## Pendientes

- valores reales del catálogo 33;
- alcance de usuario;
- collation;
- orden predeterminado;
- política de auditoría de consultas;
## Implementación inicial de búsqueda

Metadatos de referencia: SQL Server 2022 (16.0.1135.2), base `pr_t`, collation `SQL_Latin1_General_CP1_CI_AS`, compatibilidad 100 y 859,813 contratos.

`SearchAsync` implementa por ahora coincidencia exacta para número de contrato, clave de persona, RFC, VIN, tipo de operación y estatus. La consulta usa `ROW_NUMBER()` para paginación estable, con `CTO_FL_CVE` como desempate, y una consulta independiente `COUNT_BIG`.

El catálogo 33 se une por código (`PAR_CL_VALOR`) y no por descripción; por ello los códigos de estatus 7 y 10 permanecen separados aunque ambos describan `PERDIDA`. El ordenamiento se selecciona de una lista fija y todos los valores llegan como parámetros Dapper tipados.

Nombre se busca por prefijo (`LIKE @PersonNamePrefix`); VIN y solicitud se buscan por coincidencia exacta. Los códigos Legacy (`CAR_FL_CVE` para VIN y `CPC_NO_CATALOGO`/`CPC_FL_CVE` para solicitud) siguen pendientes de confirmación; ambos filtros deben permanecer desactivados en ambientes compartidos hasta confirmarlos.

## Alcance por instalación

El alcance de empresas dejó de ser un pendiente de consulta: el servidor obtiene los CompanyId activos desde Identity y filtra `KCONTRATO.EMP_FL_CVE` en búsqueda y detalle mediante `@AllowedCompanyIds`. Si no existe alcance, la operación falla cerrada antes de abrir Legacy.

## Detalle inicial de contrato

`GET /api/v1/contracts/{contractNumber}` conserva el mismo alcance de instalación y tenant que la búsqueda: `EMP_FL_CVE IN @AllowedCompanyIds`. Si el contrato no existe dentro de ese alcance, la API responde 404 sin revelar si existe en otra empresa o tenant.

La respuesta pública se proyecta mediante `ContractDetailResponse`; no expone `PersonId`, `AddressId`, `ModifiedBy`, `ModifiedAt` ni el modelo interno de Legacy. Los campos actualmente disponibles son:

- número de contrato;
- código y descripción de estado;
- código y descripción del tipo de operación;
- persona o cliente;
- monto financiado y saldo insoluto;
- fecha de desembolso, primer pago y último pago.

La consulta usa `LEFT JOIN dbo.CMONEDA AS M ON M.MON_FL_CVE = C.CTO_CL_MONEDA`, sin filtrar `MON_FG_STATUS`, para conservar contratos con referencias históricas o huérfanas. El código y nombre de moneda se conservan nulos si no existe la fila de catálogo. `CTO_NO_PLAZO` se publica como plazo actual y `CTO_NO_PLAZOORIGINAL` como plazo original; no se muestra una unidad de tiempo. `CTO_FE_INICIO` y `CTO_FE_ACTIVACION` son obligatorios y se convierten a `DateOnly` sin zona horaria. Esta etapa no escribe en Legacy.

### Campos de detalle confirmados

- `KCONTRATO.CTO_CL_MONEDA` se relaciona con `CMONEDA.MON_FL_CVE`, cuya clave es única; se publican `currencyCode` y `currencyName`.
- `KCONTRATO.CTO_NO_PLAZO` se publica como `currentTerm` y `CTO_NO_PLAZOORIGINAL` como `originalTerm`.
- `KCONTRATO.CTO_FE_INICIO` y `CTO_FE_ACTIVACION` se publican como `startDate` y `activationDate`.
- Los campos `CTO_NO_PLAZO`, `CTO_FE_INICIO` y `CTO_FE_ACTIVACION` aplican fail-fast si Dapper materializa `null`; `originalTerm` y la información de `CMONEDA` permanecen nullable.
- El `LEFT JOIN` a `CMONEDA` no agrega filas porque `MON_FL_CVE` es PK; el detalle mantiene `TOP (1)` y la búsqueda conserva una fila por contrato.

## Tabla de amortización del financiamiento

### Endpoint

```http
GET /api/v1/contracts/{contractNumber}/amortization-schedule
```

El endpoint conserva `.RequireAuthorization("contracts.read")`. Devuelve 404 si el contrato no existe dentro de `AllowedCompanyIds` o si no existe una tabla de amortización tipo 1.

### Regla Legacy y mapeo

La única fuente es `dbo.KTPAGO_CONTRATO`, con validación y alcance mediante `dbo.KCONTRATO`. El adaptador aplica `CTP_CL_TTABLA = 1`, selecciona `MAX(CTP_NO_VERSION)` por contrato y tipo, ordena por `CTP_NO_PAGO ASC` y no agrega filtro por `CTP_FG_GENERADO`.

`CTP_FG_GENERADO` se traduce a `Generated` cuando vale 1 y `Pending` cuando vale 0. `CTP_FE_EXIGIBILIDAD` es la fecha de exigibilidad y las fechas `datetime` se publican como `DateOnly`. La fila `CTP_NO_PAGO = 0` es el enganche opcional; las rentas ordinarias sólo incluyen números mayores que cero. Los importes públicos son `decimal` y se proyectan desde base, saldo posterior, amortización, interés, IVA, pago sin IVA, pago con IVA y total. Los campos `_ORIGINAL` quedan fuera de esta versión.

Las columnas requeridas fallan rápido si Dapper materializa `NULL`, evitando respuestas parciales. El DTO HTTP es explícito y no incluye el identificador Legacy del pago. El frontend muestra versión, enganche condicional y tabla responsive usando la moneda del detalle.

### Cobertura

Las pruebas cubren autorización 401/403/200, 404, versión máxima, tabla tipo 1, ausencia del filtro de generado, parametrización, separación del enganche, pagos positivos, estados, fechas y fail-fast. Las pruebas que requieren una base Legacy de prueba segura quedan omitidas mientras no exista esa conexión; no se usa una base productiva.
