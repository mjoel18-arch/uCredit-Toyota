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

## Autenticación de desarrollo

Los endpoints de contratos exigen `contracts.read`. Sólo en ambiente Development existe un esquema temporal que acepta el encabezado `X-Dev-User`; nunca debe habilitarse en producción. En otros ambientes, las rutas de contratos responden 503 hasta configurar OIDC.

## Limitaciones actuales

- La búsqueda paginada aún devuelve una colección vacía.
- El detalle por contrato tiene SQL parametrizado inicial, pendiente de validación contra la versión/collation real.
- La autenticación OIDC está pendiente de proveedor.
- No ejecutar contra producción.
