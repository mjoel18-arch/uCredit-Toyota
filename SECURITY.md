# Seguridad e identidad

## Autenticaci�n actual

La primera versi�n usa ASP.NET Core Identity con una base de seguridad independiente. La conexi�n se proporciona exclusivamente mediante `IdentitySql__ConnectionString`; nunca se reutiliza `LegacySql__ReadConnectionString` ni se aplica EF Core sobre la base Legacy.

La sesi�n usa cookie:

- HttpOnly;
- Secure fuera del ambiente de pruebas;
- SameSite=Lax;
- expiraci�n controlada y renovaci�n deslizante;
- sin tokens de acceso en localStorage ni tokens devueltos al navegador.

No existe registro p�blico ni se crean usuarios autom�ticamente. No se ejecutan migraciones ni `EnsureCreated` al iniciar la aplicaci�n.

## Protecci�n de solicitudes

Login, selección de tenant y logout requieren antiforgery mediante la cabecera `X-CSRF-TOKEN` y el token emitido por `GET /api/v1/auth/csrf`. La cookie antiforgery no contiene credenciales y no es HttpOnly para permitir que React envíe el token de solicitud en memoria.

Login y selección de tenant tienen rate limiting por dirección remota. Identity mantiene lockout después de intentos fallidos. No se registran contraseñas, hashes, security stamps, cookies ni cadenas de conexión.

## Autorizaci�n y tenants

La API conserva las políticas `contracts.read` y `customers.read`, basadas en los claims `permission` correspondientes. El usuario selecciona un tenant mediante `POST /api/v1/auth/select-tenant`; la API valida usuario activo, tenant activo y membresía activa, y emite una nueva cookie firmada con sólo los permisos de esa membresía. El tenant activo se obtiene exclusivamente de `tenant_id` y `tenant_code` en la cookie; `X-Tenant-Code` no es autoridad. La resolución de tenant no filtra todavía contratos ni selecciona conexiones Legacy.

## Secretos y bases

Est� prohibido guardar credenciales, tokens o cadenas de conexi�n reales en Git, `appsettings.json`, archivos `.env` versionados, fixtures, logs o documentaci�n. LegacySql conserva �nicamente acceso de lectura para el primer vertical.

## Entra External ID

Microsoft Entra External ID queda como alternativa OIDC postergada. Sus componentes de transformaci�n se conservan aislados como referencia hist�rica, pero no se registran como autenticaci�n productiva de esta versi�n.

## Migraciones

La primera migraci�n de Identity deber� ejecutarse mediante un procedimiento controlado sobre una base nueva y no productiva, con revisi�n del modelo, respaldo, aprobaci�n, evidencia de aplicaci�n y plan de reversa. Nunca se ejecutar� una migraci�n sobre la base Legacy.

## Pruebas del modelo Identity

Las pruebas rápidas usan `Microsoft.EntityFrameworkCore.InMemory` para verificar el metamodelo EF y el comportamiento de selección sin SQL Server. InMemory no valida realmente las FKs, los índices únicos ni otras restricciones relacionales físicas. Por eso se conserva una prueba del modelo para claves, índices y relaciones, pero la validación de restricciones reales queda pendiente para una base SQL desechable. No se deben usar esas pruebas para justificar una migración productiva.

## Revalidación de cookie

Cada solicitud autenticada con cookie consulta Identity para verificar usuario, tenant, membresía y permisos actuales. Esto agrega una consulta y costo de latencia por solicitud protegida, priorizando seguridad durante esta fase. Más adelante podrá agregarse caché de corta duración con invalidación explícita ante cambios de identidad, membresías o permisos.
## Aprovisionamiento inicial controlado

El proyecto `tools/uCredit.IdentityAdmin` permite aprovisionar únicamente la base independiente de Identity en desarrollo. Exige `DOTNET_ENVIRONMENT=Development`, el argumento explícito `--apply`, `IdentitySql__ConnectionString` y un nombre de base terminado en `_Dev`. Rechaza producción, conexiones sin base, tenant o correo inválidos y contraseñas ausentes.

La herramienta verifica que la migración `InitialIdentity` ya esté registrada y falla si la base no está preparada. No ejecuta `Migrate`, `EnsureCreated`, `database update` ni crea bases automáticamente. Usa `UserManager<ApplicationUser>` para crear el usuario y una transacción sobre Identity cuando la operación se ejecuta.

La configuración de desarrollo se recibe por variables de entorno o User Secrets mediante:

- `UCREDIT_BOOTSTRAP_TENANT_CODE`;
- `UCREDIT_BOOTSTRAP_TENANT_NAME`;
- `UCREDIT_BOOTSTRAP_ADMIN_EMAIL`;
- `UCREDIT_BOOTSTRAP_ADMIN_PASSWORD`;
- `UCREDIT_BOOTSTRAP_COMPANY_IDS` (lista separada por comas de enteros 0 a 255).

La operación es idempotente y no cambia la contraseña de un usuario existente. En cada ejecución garantiza la existencia de `contracts.read` y `customers.read` y los asigna únicamente a la membresía activa del administrador en el tenant indicado. Las asignaciones están vinculadas por `UserId`, `TenantId` y `PermissionId`; no se copian permisos a otros tenants ni se eliminan permisos existentes. La salida sólo muestra identificadores de objetos y si fueron creados o ya existían; nunca muestra contraseñas, hashes, security stamps, tokens o cadenas de conexión. No se deben agregar valores reales a Git ni ejecutar la herramienta contra Legacy.

Antes de ejecutar el aprovisionamiento, la migracion `AddTenantLegacyCompanyScope` debe estar aplicada en la base Identity. La herramienta nunca ejecuta `Migrate`, `EnsureCreated` ni `database update`. `UCREDIT_BOOTSTRAP_COMPANY_IDS` solo agrega o reactiva scopes para el tenant indicado; no desactiva scopes ausentes de la lista. `DisplayName` queda nulo y no se consulta `CEMPRESA` ni ninguna base Legacy.

## Aislamiento Legacy por instalación

Cada instalación tiene una única base Legacy y una única base Identity. `LegacySql__ReadConnectionString` es la única conexión de lectura Legacy y nunca se guarda en Identity. `Deployment__TenantCode` debe identificar exactamente el tenant permitido por el sitio; si falta, la selección y las consultas Legacy fallan cerradas.

Identity almacena únicamente el alcance lógico de empresas mediante `TenantLegacyCompanyScopes`: `TenantId`, `CompanyId` (el valor de `KCONTRATO.EMP_FL_CVE`), nombre opcional y estado activo. El servidor obtiene los CompanyId activos desde Identity. El frontend no puede enviar una lista, query string, cabecera o conexión para ampliar el alcance.

`SearchAsync` y `GetByNumberAsync` agregan `C.EMP_FL_CVE IN @AllowedCompanyIds` con parámetros Dapper. La consulta no crea una conexión Legacy si no existe tenant seleccionado, el tenant no corresponde a `Deployment__TenantCode` o no hay empresas activas.
