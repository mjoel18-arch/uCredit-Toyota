# Administración de domicilios

Este módulo administra de forma controlada los domicilios de `Customer`.
No crea una entidad Legacy nueva y no elimina físicamente registros de
`dbo.CDOMICILIO`.

## Contrato HTTP

```text
GET  /api/v1/customers/{personId}/addresses
POST /api/v1/customers/{personId}/addresses
PUT  /api/v1/customers/{personId}/addresses/{addressId}
POST /api/v1/customers/{personId}/addresses/{addressId}/activate
POST /api/v1/customers/{personId}/addresses/{addressId}/deactivate
```

GET requiere `customers.read`. Las mutaciones requieren `customers.write`,
antiforgery, tenant seleccionado, tenant coincidente con
`Deployment__TenantCode`, entorno Development, `LegacyUserCode` de la
membresía activa, conexión Legacy de escritura y las guardas explícitas de
`pr_t`.

`personId` y `addressId` sólo se aceptan desde la ruta. El body nunca puede
reemplazarlos ni incluir nombres de columnas Legacy.

El contrato utiliza `uses`, con valores exclusivos:

- `billing` — Facturación;
- `statements` — Estado de cuenta;
- `other` — Otros.

El backend normaliza mayúsculas/minúsculas, recorta espacios, elimina
duplicados y rechaza valores desconocidos.

## Matriz funcional

| Tipo | Facturación | Estado de cuenta | Otros |
|---|---|---|---|
| 1 Dirección única | Obligatorio y bloqueado | Obligatorio y bloqueado | Obligatorio y bloqueado |
| 2 Dirección fiscal | Obligatorio y bloqueado | Opcional | Opcional |
| 3 Dirección administrativa | No disponible | Opcional | Opcional |
| 4 Dirección social | No disponible | Opcional | Opcional |

El backend deriva `DMO_FG_FACTURA`, `DMO_FG_EDOCTA` y `DMO_FG_OTROS`; nunca
los recibe del navegador.

## Persistencia Legacy

El repositorio Dapper usa únicamente parámetros tipados. El mapeo público es:

| Propiedad | Columna |
|---|---|
| `addressId` | `DMO_FL_CVE` |
| `personId` | `PNA_FL_PERSONA` |
| `postalCode` | `DMO_CL_CPOSTAL` |
| `state` | `DMO_DS_EFEDERATIVA` |
| `municipality` | `DMO_DS_MUNICIPIO` |
| `city` | `DMO_DS_CIUDAD` |
| `neighborhood` | `DMO_DS_COLONIA` |
| `streetAndNumber` | `DMO_DS_CALLE_NUM` |
| `exteriorNumber` | `DMO_DS_NUMEXT` |
| `interiorNumber` | `DMO_DS_NUMINT` |
| `addressTypeCode` | `DMO_FG_TDIRECCION` |
| `isActive` | `DMO_FG_STATUS = 1` |
| `isDefault` | `DMO_FG_REGDEFAULT = 1` |
| `modifiedAt` | `DMO_FE_ULTMOD` |
| `countryCode` | `PAI_FL_CVE` |

El alta reserva `DMO_FL_CVE` mediante `CCATCONSEC` dentro de la transacción.
PUT, activar y desactivar no generan consecutivos.

## Predeterminado y concurrencia

Toda persona con domicilios debe conservar exactamente un domicilio activo
predeterminado. El primero se crea activo y predeterminado. Crear o editar
otro con `isDefault=true` desmarca el anterior dentro de la misma
transacción.

No se permite quitar el predeterminado actual sin un reemplazo activo de la
misma persona. La desactivación requiere:

```json
{
  "expectedModifiedAt": "2026-09-20T12:00:00.0000000",
  "replacementAddressId": 123
}
```

PUT, activar y desactivar comparan `expectedModifiedAt` con
`DMO_FE_ULTMOD` usando `DMO_FL_CVE` y `PNA_FL_PERSONA`. Si no se actualiza
ninguna fila se devuelve `409 address_modified`. Sin reemplazo válido al
desactivar el predeterminado se devuelve `409 address_default_required`.

Las columnas afectadas y la bitácora se actualizan en una única transacción.
La actividad de alta es `4`; edición, activación, desactivación y cambio de
predeterminado usan `5`. Los logs sólo incluyen etapa, tipo de excepción,
número SQL cuando corresponde y correlation ID; nunca incluyen domicilio,
referencias, códigos postales, payloads o `LegacyUserCode`.

## Respuestas y UI

- `200`: consulta o mutación exitosa;
- `201`: domicilio creado;
- `400`: payload, tipo o uso inválido;
- `401`: usuario no autenticado;
- `403`: permiso o tenant inválido;
- `404`: cliente o domicilio inexistente;
- `409`: conflicto de predeterminado o concurrencia;
- `503`: escritura no configurada o base distinta de `pr_t`.

El expediente muestra todos los domicilios, estado activo/inactivo,
predeterminado y usos con etiquetas amigables. El panel lateral es accesible,
evita doble envío, confirma la desactivación y solicita reemplazo cuando
corresponde. Después de cada mutación se refrescan domicilios y readiness sin
recargar la página. No se usa `localStorage` ni `sessionStorage`.

## Pruebas

La cobertura debe incluir lista vacía y múltiple, primer predeterminado,
cambio de predeterminado, matriz de usos, duplicados y valores desconocidos,
concurrencia, desactivación con y sin reemplazo, rollback, bitácora,
autorización, antiforgery, tenant incorrecto, códigos HTTP, ausencia de PII
en logs, actualización de readiness, doble envío y accesibilidad básica del
panel.
