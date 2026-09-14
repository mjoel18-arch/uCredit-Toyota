# Compilar y ejecutar

## Requisitos

- .NET SDK 10.
- Node.js 22 o posterior.
- npm.
- SQL Server local/QA para la integración Legacy.

## Backend

```bash
dotnet restore uCredit.slnx
dotnet build uCredit.slnx --no-restore
dotnet test uCredit.slnx --no-build
dotnet run --project src/uCredit.Api
```

La cadena `LegacySql:ReadConnectionString` debe proporcionarse mediante Secret Manager o variable de ambiente. No editar `appsettings.json` con credenciales.

La API requiere configurar `Authentication:Authority`, `Authentication:Audience` y `Authentication:ClientId` mediante configuración segura. Variables equivalentes: `Authentication__Authority`, `Authentication__Audience` y `Authentication__ClientId`. No guardar valores reales en Git.

Ejemplo de clave de variable de ambiente:

```text
LegacySql__ReadConnectionString
```

## Frontend

```bash
cd src/uCredit.Web
npm install
npm run build
npm run dev
```

## Branding de demostración

El frontend pide `/api/v1/branding/current`. El esqueleto envía temporalmente `X-Tenant-Code: DEMO` para mostrar el cambio de tema. Esto debe reemplazarse con resolución segura de tenant antes de un despliegue compartido.

## Autenticación

Los endpoints de contratos exigen `contracts.read`. Fuera del entorno exclusivo de pruebas, la API usa JWT Bearer con Microsoft Entra External ID y requiere la configuración de autoridad, audiencia y client ID. El entorno de pruebas sustituye explícitamente la autenticación mediante `WebApplicationFactory`.

## Limitaciones actuales

- La búsqueda paginada aún devuelve una colección vacía.
- El detalle por contrato tiene SQL parametrizado inicial, pendiente de validación contra la versión/collation real.
- La autenticación OIDC está pendiente de proveedor.
- No ejecutar contra producción.
