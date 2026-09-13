# Estado de validación del esqueleto

Fecha: 13 de septiembre de 2026

## Validado

- Estructura de archivos y referencias revisada.
- Archivos JSON analizados sintácticamente.
- Instalación de dependencias frontend completada.
- Compilación TypeScript completada.
- Build de producción de Vite completado.
- No se incluyeron cadenas de conexión ni credenciales.
- SQL de detalle usa el parámetro `@ContractNumber`.
- El paquete excluye `node_modules`, `dist`, `bin` y `obj`.


## Cambios de esta validación

- Se agregó `uCredit.Modules.Contracts.IntegrationTests` a `uCredit.slnx`.
- El proyecto de integración usa xUnit y referencia `uCredit.Modules.Contracts` y `uCredit.Infrastructure.LegacySql`.
- Se agregó una prueba inicial de consulta omitida explícitamente porque aún no existe una conexión segura de pruebas.
- Se ampliaron las pruebas de arquitectura para validar límites entre Contracts, Branding, API y LegacySql, además de prohibir `System.Web`, `DataSet` y `DataTable` en módulos funcionales.
- No se implementaron operaciones de escritura, no se agregaron cadenas de conexión y no se realizaron conexiones a SQL Server.

## Resultado backend

```text
dotnet build uCredit.slnx: bloqueado por `Access denied` al escribir artefactos `obj` preexistentes
dotnet test uCredit.slnx: 3 pruebas ejecutadas correctamente (1 arquitectura, 2 unitarias); la integración no pudo ejecutarse porque su DLL no llegó a compilar por el bloqueo de `obj`
```
## Pendiente

- Repetir la validación backend cuando se liberen los artefactos `obj` bloqueados por el entorno.
- Restaurar paquetes NuGet desde el repositorio autorizado por TI.
- Validar consulta contra una copia local/QA.
- Confirmar versión y collation de SQL Server.
- Configurar proveedor OIDC para ambientes compartidos.
- Validar la búsqueda paginada contra una copia local/QA con datos representativos.
- Confirmar versiones de paquetes con la política corporativa antes del primer merge.

## Resultado frontend

```text
TypeScript: aprobado
Vite production build: aprobado
```

El build generado se usó sólo para validación y no forma parte del paquete fuente.
## Consulta de contratos

- Se implementó `LegacyContractReadRepository.SearchAsync` como operación de sólo lectura.
- Metadatos registrados: SQL Server 2022 (16.0.1135.2), base `pr_t`, collation `SQL_Latin1_General_CP1_CI_AS`, compatibilidad 100 y 859,813 contratos.
- La paginación usa `ROW_NUMBER()` con `CTO_FL_CVE` como desempate y el total usa una consulta independiente `COUNT_BIG`.
- Se admiten coincidencias exactas para contrato, persona, RFC, tipo de operación y estatus.
- Nombre, VIN y solicitud se rechazan hasta confirmar índices y semántica parcial.
- No se agregaron cadenas de conexión ni se realizó conexión a SQL Server.
