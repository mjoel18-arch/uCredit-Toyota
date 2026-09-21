# Módulo Customers — Consulta de clientes

## Alcance

La primera etapa expone sólo lectura mediante:

- `GET /api/v1/customers`
- `GET /api/v1/customers/{personId}`

La entidad funcional es `Customer`. “Prospecto” sólo aparece como título heredado del Legacy; no existe una entidad separada. El identificador central es `CPERSONA.PNA_FL_PERSONA`.

Las altas, modificaciones, propuestas y contratos quedan fuera de esta etapa. No se habilita escritura en Legacy.

## Trazabilidad Legacy

`su_MtoPersona.aspx` → evento de consulta → `sn_clsPersona` / `sd_clsPersona` → `CPERSONA`, `CPFISICA`, `CPMORAL`, `CPTIPO` y tablas relacionadas.

El repositorio moderno encapsula el SQL Dapper en `LegacyCustomerReadRepository`; el módulo `Customers` sólo conoce contratos y modelos de lectura. La elegibilidad para contratos se consulta aparte mediante `GET /api/v1/customers/{personId}/readiness`; usa `EXISTS` para verificar únicamente estatus activo de persona, domicilio, teléfono y `CPCUENTA`, sin devolver cuentas, CLABE ni PII adicional.

## Mapeo confirmado

- `CPERSONA.PNA_FL_PERSONA` → `personId`.
- `CPERSONA.PNA_CL_RFC` (`varchar(13)`) → RFC; en listas se enmascara.
- Persona física: `CPFISICA.PFI_DS_NOMBRE`, `PFI_DS_APATERNO`, `PFI_DS_AMATERNO`.
- Persona moral: `CPMORAL.PMO_DS_RAZON_SOCIAL`.
- `CPERSONA.PNA_CL_PJURIDICA` + `CPARAMETRO` catálogo `6` → personalidad jurídica.
- `CPERSONA.PNA_FG_STATUS` + `CPARAMETRO` catálogo `1` → estatus.
- `CPTIPO.PTI_FG_VALOR` + `CPARAMETRO` catálogo `5` → roles activos.
- Domicilio principal: `CDOMICILIO.DMO_FG_STATUS = 1` y `DMO_FG_REGDEFAULT = 1`; tipo descrito por catálogo `7`.
- Teléfonos activos: `CTELEFONO.TFN_FG_STATUS = 1`; el principal es `TFN_FG_REGDEFAULT = 1`, ordenando defensivamente por `TFN_FG_REGDEFAULT DESC, TFN_FL_CVE ASC`.
- Correos del detalle: `CPERSONA_EMAIL.MAI_FG_STATUS = 1`. No se define correo principal.

Las consultas cargan colecciones relacionadas por separado y no hacen joins que multipliquen clientes. La base Legacy no tiene una restricción única que garantice un único teléfono predeterminado; la consulta conserva todos los teléfonos activos y selecciona el menor `TFN_FL_CVE` entre los predeterminados.

## Seguridad y validación

La política es `customers.read`. El tenant se resuelve mediante la cookie firmada existente; no se aceptan headers de tenant. Las búsquedas exigen `personId`, RFC o nombre, con RFC máximo de 13 caracteres, nombre máximo de 200 y `pageSize` máximo de 100. No se registran RFC completos, teléfonos, correos ni domicilios.

## Pendiente

La creación y mantenimiento de Customer, la creación de propuestas, el alta directa de contratos y la conversión explícita de propuesta autorizada a contrato requieren autorización funcional y de escritura Legacy posterior.
# Teléfonos del expediente

El detalle de Customer obtiene la colección de teléfonos desde el endpoint
de administración de teléfonos. La selección del principal sigue siendo
defensiva: teléfonos activos predeterminados ordenados por `TFN_FL_CVE ASC`.
Los valores Legacy se validan en el adaptador y no se registran como PII.
