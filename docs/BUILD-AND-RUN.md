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

Para la demostración Toyota, el tenant `TOYOTA` devuelve `productName: uCredit-auto`, `customerName: Toyota Financial Services`, colores configurables, título `uCredit-auto | Toyota Financial Services` y la ruta local autorizada `/branding/toyota/logo.png`. El PNG oficial proporcionado por el cliente debe permanecer en `src/uCredit.Web/public/branding/toyota/logo.png`; no se convierte ni se renombra. Si el archivo falla, la UI usa el fallback textual `uCredit-auto` y no muestra un icono roto. Las pantallas futuras de Cliente / Prospecto y Captura de contrato ya reservan su identidad visual con el mismo tema.

## Autenticación

Los endpoints de contratos exigen `contracts.read`. La primera versión usa ASP.NET Core Identity con una cookie HttpOnly, Secure fuera de pruebas y SameSite=Lax. Login, selección de tenant y logout usan antiforgery; login y selección tienen rate limiting. La selección valida membresía activa y regenera la cookie con permisos sólo del tenant seleccionado. El entorno de pruebas sustituye explícitamente la autenticación mediante `WebApplicationFactory`. Entra External ID queda como alternativa OIDC postergada.

## Limitaciones actuales

- La búsqueda paginada aún devuelve una colección vacía.
- El detalle por contrato tiene SQL parametrizado inicial, pendiente de validación contra la versión/collation real.
- La selección segura de tenant está implementada; la aplicación aún no filtra contratos ni selecciona conexiones Legacy por tenant.
- La autenticación OIDC está postergada, pero la arquitectura conserva un punto de sustitución futuro.
- No ejecutar `EnsureCreated` ni `Migrate` al iniciar; la primera migración de Identity requerirá revisión y ejecución controlada contra una base nueva.
- No ejecutar contra producción.

## Aprovisionamiento inicial de Identity

`tools/uCredit.IdentityAdmin` es una herramienta controlada para una base nueva de desarrollo. No ejecuta migraciones: exige que `InitialIdentity` ya esté aplicada y rechaza cualquier base cuyo nombre no termine en `_Dev`.

Antes de ejecutarla posteriormente, deben estar configurados `DOTNET_ENVIRONMENT=Development` e `IdentitySql__ConnectionString` mediante el entorno seguro. Los siguientes valores pueden proporcionarse por variables de entorno o User Secrets, nunca en Git:

- `UCREDIT_BOOTSTRAP_TENANT_CODE`
- `UCREDIT_BOOTSTRAP_TENANT_NAME`
- `UCREDIT_BOOTSTRAP_ADMIN_EMAIL`
- `UCREDIT_BOOTSTRAP_ADMIN_PASSWORD`
- `UCREDIT_BOOTSTRAP_COMPANY_IDS` (por ejemplo, `1`; lista separada por comas, sin duplicados, rango 0-255)

Ejemplo de configuración local con User Secrets, usando valores locales no versionados:

```bash
dotnet user-secrets --project tools/uCredit.IdentityAdmin set "UCREDIT_BOOTSTRAP_TENANT_CODE" "<valor-local>"
dotnet user-secrets --project tools/uCredit.IdentityAdmin set "UCREDIT_BOOTSTRAP_TENANT_NAME" "<valor-local>"
dotnet user-secrets --project tools/uCredit.IdentityAdmin set "UCREDIT_BOOTSTRAP_ADMIN_EMAIL" "<valor-local>"
dotnet user-secrets --project tools/uCredit.IdentityAdmin set "UCREDIT_BOOTSTRAP_ADMIN_PASSWORD" "<valor-local>"
dotnet user-secrets --project tools/uCredit.IdentityAdmin set "UCREDIT_BOOTSTRAP_COMPANY_IDS" "<company-ids>"
```

La ejecución posterior debe ser explícita:

```bash
dotnet run --project tools/uCredit.IdentityAdmin -- --apply
```

La herramienta es idempotente: reutiliza tenant, permiso, usuario, membresía y asignación existentes. Nunca cambia silenciosamente la contraseña de un usuario existente y no imprime contraseñas, hashes, stamps, tokens ni cadenas de conexión.
También exige que `AddTenantLegacyCompanyScope` ya esté aplicada. Para cada CompanyId indicado crea o reactiva el scope del tenant; no elimina ni desactiva scopes omitidos. No consulta Legacy ni ejecuta migraciones.

## Smoke test de Identity local

El flujo real de autenticación local puede verificarse con:

```powershell
pwsh -NoProfile -File .\scripts\Test-LocalIdentity.ps1
```

El script solicita interactivamente la URL de la API, el correo y la contraseña mediante `Read-Host -AsSecureString`. Mantiene cookies únicamente en una `WebRequestSession` en memoria y no imprime cookies, tokens CSRF ni contraseñas. Puede recibir opcionalmente `UCREDIT_TEST_CONTRACT`; si no está configurada, solicita el contrato y usa `454890CD` como valor sugerido. Después de seleccionar tenant verifica el detalle y la búsqueda exacta sin imprimir datos personales ni financieros.

Antes de ejecutarlo posteriormente, la API debe estar disponible y el usuario de desarrollo debe tener una membresía activa en `ubimia-dev` con `contracts.read`. El script no consulta contratos ni SQL Server directamente. Falla ante cualquier estado HTTP inesperado y confirma que `/api/v1/auth/me` devuelve 401 después del logout.

## Aislamiento Legacy por instalación

Cada sitio requiere la variable segura `Deployment__TenantCode`, exactamente igual al código del tenant permitido. No se debe configurar una conexión Legacy por tenant ni enviar CompanyId desde el navegador. La única conexión Legacy continúa siendo `LegacySql__ReadConnectionString`; el alcance de empresas se administra en Identity.
## Desarrollo local del frontend con HTTPS

Vite exige un certificado local explícito para que las cookies Secure de Identity funcionen durante el desarrollo. La API debe ejecutarse con su perfil HTTPS y el frontend se sirve también por HTTPS; no se habilita HTTP como alternativa.

Con mkcert instalado, PowerShell puede preparar certificados locales fuera del repositorio:

    $certDir = Join-Path $env:LOCALAPPDATA 'uCredit\certs'
    New-Item -ItemType Directory -Force -Path $certDir | Out-Null
    mkcert -install
    mkcert -cert-file (Join-Path $certDir 'localhost.pem') -key-file (Join-Path $certDir 'localhost-key.pem') localhost 127.0.0.1 ::1
    $env:VITE_DEV_HTTPS_CERT = Join-Path $certDir 'localhost.pem'
    $env:VITE_DEV_HTTPS_KEY = Join-Path $certDir 'localhost-key.pem'

En otra terminal, inicia la API y después Vite:

    dotnet run --project src/uCredit.Api --launch-profile https
    cd src/uCredit.Web
    npm install
    npm run dev -- --host localhost

Abre https://localhost:5173. El proxy de Vite reenvía /api y /health a https://localhost:7042; VITE_API_BASE_URL queda vacío por defecto para mantener las solicitudes same-origin. Si se configura un API base externo, el backend debe permitir únicamente ese origen HTTPS concreto mediante CORS explícito; nunca se deben usar wildcard ni credenciales con *.

El cliente React usa credentials: include en todas las solicitudes, solicita un token antiforgery nuevo para cada mutación y mantiene el token sólo durante la llamada. No usa localStorage ni sessionStorage para contraseñas, cookies, tokens o respuestas de autenticación. Una respuesta 401 devuelve al login; una 403 muestra acceso no autorizado sin redirecciones.

Pruebas y build del frontend:

    cd src/uCredit.Web
    npm run lint
    npm run test
    npm run build

La variable opcional VITE_API_BASE_URL no contiene secretos. No se debe colocar una contraseña, token, cookie o cadena de conexión en variables VITE_*, porque Vite las expone al navegador.
## Diagnóstico y smoke test del HTTPS efectivo de Vite

El script scripts/Test-ViteHttps.ps1 inicia temporalmente npm run dev con --config vite.config.ts, espera la línea Local: y falla si el protocolo anunciado es HTTP, si Vite termina antes de iniciar o si falta el certificado. Usa una caché temporal fuera del repositorio y limpia el proceso y sus archivos al terminar.

Con las variables de entorno de certificados ya configuradas, el comando exacto en Windows PowerShell es:

    .\scripts\Test-ViteHttps.ps1

El resultado esperado es:

    Local: https://localhost:5173/
    Vite HTTPS smoke test passed: https://localhost:5173/

vite.config.ts recibe command=serve durante npm run dev y command=build durante npm run build. Como el repositorio conserva un vite.config.js generado que puede ser elegido por Vite al usar el comando sin --config, los scripts dev y build fijan explícitamente --config vite.config.ts. No se imprime ninguna ruta de certificado ni contenido de la llave privada.

loadEnv(mode, process.cwd(), '') carga las variables VITE_DEV_HTTPS_CERT y VITE_DEV_HTTPS_KEY para la configuración. Durante serve, la ausencia de cualquiera de las variables, un archivo inexistente, un PEM inválido o un par certificado/llave que no coincida detiene Vite antes de abrir el puerto. server.strictPort también impide cambiar silenciosamente de 5173.
## Integración continua

`.github/workflows/ci.yml` valida cada Pull Request hacia `main` y cada push a `main` o `feature/**`. Ejecuta en jobs separados el restore, build y test de toda la solución .NET, además de `npm ci`, lint, pruebas y build del frontend con Node.js 22. El workflow sólo usa `contents: read`, cancela ejecuciones obsoletas por referencia y no configura cadenas de conexión, secretos, SQL Server, migraciones, IdentityAdmin ni smoke tests que requieran servicios activos. Las pruebas de integración Legacy se omiten automáticamente mientras no existan variables seguras de prueba.
