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

`KCONTRATO`, `KDESEMBOLSO`, `KTOPERACION`, `CPARAMETRO`, `CPERSONA`, `CEMPRESA`, `CUSUARIO`, `KCARAC_PROD_FACT`, `KPRODUCTO_FACTURA` y `CCATALOGO_CONTRATO`.

## Endpoint

```http
GET /api/v1/contracts
```

Parámetros: `contractNumber`, `personId`, `rfc`, `personName`, `vin`, `operationType`, `status`, `page`, `pageSize` y `sort`.

Exigir al menos un criterio principal, salvo permiso especial. `pageSize` debe estar limitado.

```http
GET /api/v1/contracts/{contractNumber}
```

Devuelve el resumen inicial de sólo lectura.

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
- Filtros de empresa/cartera pendientes de confirmar.
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
- campos exactos del detalle inicial.
## Implementación inicial de búsqueda

Metadatos de referencia: SQL Server 2022 (16.0.1135.2), base `pr_t`, collation `SQL_Latin1_General_CP1_CI_AS`, compatibilidad 100 y 859,813 contratos.

`SearchAsync` implementa por ahora coincidencia exacta para número de contrato, clave de persona, RFC, VIN, tipo de operación y estatus. La consulta usa `ROW_NUMBER()` para paginación estable, con `CTO_FL_CVE` como desempate, y una consulta independiente `COUNT_BIG`.

El catálogo 33 se une por código (`PAR_CL_VALOR`) y no por descripción; por ello los códigos de estatus 7 y 10 permanecen separados aunque ambos describan `PERDIDA`. El ordenamiento se selecciona de una lista fija y todos los valores llegan como parámetros Dapper tipados.

Nombre se busca por prefijo (`LIKE @PersonNamePrefix`); VIN y solicitud se buscan por coincidencia exacta. Los códigos Legacy (`CAR_FL_CVE` para VIN y `CPC_NO_CATALOGO`/`CPC_FL_CVE` para solicitud) siguen pendientes de confirmación; ambos filtros deben permanecer desactivados en ambientes compartidos hasta confirmarlos.
