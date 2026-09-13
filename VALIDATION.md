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
- No se implementó `SearchAsync`, no se agregó ninguna cadena de conexión y no se realizaron conexiones a SQL Server.

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
- Implementar búsqueda paginada; el método actual devuelve una página vacía intencionalmente.
- Confirmar versiones de paquetes con la política corporativa antes del primer merge.

## Resultado frontend

```text
TypeScript: aprobado
Vite production build: aprobado
```

El build generado se usó sólo para validación y no forma parte del paquete fuente.
