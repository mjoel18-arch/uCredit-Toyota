# ADR-008: Aislamiento Legacy por instalación y tenant

- Estado: Aprobado
- Fecha: 2026-09-15

## Contexto

Cada cliente opera un sitio uCredit independiente, una base Legacy propia y una base Identity propia. Un sitio puede administrar una o varias carteras de su cliente, identificadas por `KCONTRATO.EMP_FL_CVE`. No existe un escenario válido en el que el usuario elija una conexión Legacy o cambie de instalación desde el navegador.

## Decisión

La instalación usa exclusivamente `LegacySql__ReadConnectionString` para lectura de su propia base Legacy. `Deployment__TenantCode` identifica el único tenant permitido por ese sitio y debe configurarse desde una fuente segura de configuración.

La base Identity relaciona cada `Tenant` con cero o más `TenantLegacyCompanyScope`, cada uno con `CompanyId` correspondiente a `EMP_FL_CVE`, nombre descriptivo opcional y estado activo. La clave es `TenantId + CompanyId` y la FK hacia `Tenant` es restrictiva.

La cookie firmada sigue siendo la única autoridad para el tenant seleccionado. La API valida que el tenant de la membresía coincida exactamente con `Deployment__TenantCode`. El frontend no puede enviar el alcance de empresas ni usar `X-Tenant-Code`, query string o una lista de empresas como autoridad.

Las consultas Legacy reciben un contexto neutral de ejecución resuelto en el servidor. `SearchAsync` y `GetByNumberAsync` agregan obligatoriamente `C.EMP_FL_CVE IN @AllowedCompanyIds` con parámetros Dapper. Si falta el tenant de despliegue, no hay tenant seleccionado o no existen empresas activas, la operación falla cerrada antes de crear una conexión Legacy.

## Dependencias

La abstracción `IExecutionTenantContext` vive en `uCredit.Application`. Identity la implementa y LegacySql la consume. Contracts conserva sus interfaces y no referencia Identity ni LegacySql. Identity y LegacySql no se referencian entre sí.

## Consecuencias

- El aislamiento se decide por configuración de instalación y datos de Identity, nunca por entrada del navegador.
- Una instalación sólo puede consultar sus carteras autorizadas en su propia base Legacy.
- La revocación de tenant, membresía o empresa activa impide nuevas consultas sin modificar Legacy.
- La lista de empresas no se expone como una instrucción ejecutable del frontend.
- La validación de FKs e índices físicos de la nueva tabla requiere una base Identity SQL desechable; no se ejecuta como parte de esta fase.
- Futuras necesidades de otra instalación o conexión requieren una decisión arquitectónica explícita y no se resolverán con selección dinámica de usuario.
