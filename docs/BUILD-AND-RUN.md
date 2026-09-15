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

El frontend pide `/api/v1/branding/current`. El endpoint usa únicamente el tenant seleccionado en la cookie firmada; `X-Tenant-Code` no se acepta como autoridad. Sin selección, se devuelve el tema predeterminado.

## Autenticación

Los endpoints de contratos exigen `contracts.read`. La primera versión usa ASP.NET Core Identity con una cookie HttpOnly, Secure fuera de pruebas y SameSite=Lax. Login, selección de tenant y logout usan antiforgery; login y selección tienen rate limiting. La selección valida membresía activa y regenera la cookie con permisos sólo del tenant seleccionado. El entorno de pruebas sustituye explícitamente la autenticación mediante `WebApplicationFactory`. Entra External ID queda como alternativa OIDC postergada.

## Limitaciones actuales

- La búsqueda paginada aún devuelve una colección vacía.
- El detalle por contrato tiene SQL parametrizado inicial, pendiente de validación contra la versión/collation real.
- La selección segura de tenant está implementada; la aplicación aún no filtra contratos ni selecciona conexiones Legacy por tenant.
- La autenticación OIDC está postergada, pero la arquitectura conserva un punto de sustitución futuro.
- No ejecutar `EnsureCreated` ni `Migrate` al iniciar; la primera migración de Identity requerirá revisión y ejecución controlada contra una base nueva.
- No ejecutar contra producción.
