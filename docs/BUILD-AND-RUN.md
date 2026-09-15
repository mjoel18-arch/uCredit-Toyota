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

La API requiere configurar `IdentitySql:ConnectionString` mediante configuración segura. La variable equivalente es `IdentitySql__ConnectionString`; corresponde exclusivamente a la base nueva de seguridad y nunca debe apuntar a Legacy. No guardar valores reales en Git.

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

Los endpoints de contratos exigen `contracts.read`. La primera versión usa ASP.NET Core Identity con una cookie HttpOnly, Secure fuera de pruebas y SameSite=Lax. Login y logout usan antiforgery; login tiene rate limiting y lockout. El entorno de pruebas sustituye explícitamente la autenticación mediante `WebApplicationFactory`. Entra External ID queda como alternativa OIDC postergada.

## Limitaciones actuales

- La búsqueda paginada aún devuelve una colección vacía.
- El detalle por contrato tiene SQL parametrizado inicial, pendiente de validación contra la versión/collation real.
- La resolución segura de tenant y el cálculo de permisos por membresía activa están pendientes; no se debe confiar en un tenant enviado por el navegador.
- La autenticación OIDC está postergada, pero la arquitectura conserva un punto de sustitución futuro.
- No ejecutar `EnsureCreated` ni `Migrate` al iniciar; la primera migración de Identity requerirá revisión y ejecución controlada contra una base nueva.
- No ejecutar contra producción.
