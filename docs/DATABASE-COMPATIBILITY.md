# Compatibilidad con SQL Server Legacy

## Objetivo

Permitir que uCredit y ProLeaseNet convivan sin alterar contratos de datos utilizados por clientes, reportes, procedimientos e integraciones.

## Inventario de referencia

- 661 tablas.
- 727 procedimientos almacenados.
- 23 vistas.
- 16 funciones.
- 9 tipos de tabla.
- 26 llaves foráneas declaradas.

La baja cantidad de llaves foráneas indica relaciones implícitas que deben documentarse desde código y procedimientos.

## Política

La BD Legacy es de **compatibilidad**, no el modelo de dominio de uCredit. Los nombres `CTO_*`, `PNA_*`, etc. se traducen en la capa `LegacySql` y no se propagan al resto de la solución.

## Prohibiciones

- migraciones automáticas;
- cambios de tipos o nulabilidad sin aprobación;
- renombrar/eliminar objetos;
- SQL concatenado;
- pruebas con producción;
- credenciales en Git;
- índices sin plan y evidencia;
- uso indiscriminado de `NOLOCK`.

## Conexiones

| Nombre lógico | Propósito | Privilegio |
|---|---|---|
| `LegacyRead` | Consultas | SELECT mínimo |
| `LegacyWrite` | Comandos futuros | Mínimo por caso de uso |
| `uCreditStore` | Datos exclusivos futuros | Mediante migraciones controladas |

El primer vertical sólo usa `LegacyRead`.

## Flujo de cambio SQL

1. HU y justificación.
2. Inventario de consumidores Legacy/uCredit.
3. Script de avance y reversa.
4. Pruebas sobre copia representativa.
5. Revisión DBA.
6. Evidencia de rendimiento.
7. Aprobación.
8. Ejecución por pipeline/proceso controlado.
9. Validación posterior.

## Mapeo inicial

| Legacy | uCredit |
|---|---|
| `KCONTRATO.CTO_FL_CVE` | `ContractNumber` |
| `KCONTRATO.PNA_FL_PERSONA` | `PersonId` |
| `KCONTRATO.CTO_NO_MTO_FINANCIAR` | `FinancedAmount` |
| `KCONTRATO.CTO_NO_SALDO` | `OutstandingBalance` |
| `KCONTRATO.CTO_FG_STATUS` | `ContractStatusCode` |
| `KCONTRATO.CTO_FE_ULTMOD` | `ModifiedAt` |
| `CUSUARIO.USR_DS_NOMBRE` | `ModifiedByName` |

## Objetos críticos iniciales

`KCONTRATO`, `CPERSONA`, `CFECHA_OPERACION`, `KMOVIMIENTO`, `KTPAGO_CONTRATO`, `CPARAMETRO`, `KPAGO`, `KPAGO_MOVIMIENTO`, `KTOPERACION`, `CUSUARIO` y `CEMPRESA`.

## Validaciones pendientes

- versión mínima de SQL Server por cliente;
- collation y sensibilidad a mayúsculas/acentos;
- volúmenes por tabla;
- índices y planes de búsqueda;
- bases independientes o multitenencia;
- objetos cifrados/no incluidos;
- jobs, linked servers y dependencias externas;
- descripción de catálogos 4 y 33.
## Metadatos de la validación de contratos

La consulta inicial se diseñó para SQL Server 2022 (16.0.1135.2), sobre la base `pr_t`, con collation `SQL_Latin1_General_CP1_CI_AS`, nivel de compatibilidad 100 y un volumen de referencia de 859,813 contratos.

La paginación usa `ROW_NUMBER()` y `CTO_FL_CVE` como desempate estable; no usa `OFFSET/FETCH`. La consulta de total es independiente y usa `COUNT_BIG`. No se modifican objetos, compatibilidad, tablas, vistas, procedimientos ni índices.
