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

Login y logout requieren antiforgery mediante la cabecera `X-CSRF-TOKEN` y el token emitido por `GET /api/v1/auth/csrf`. La cookie antiforgery no contiene credenciales y no es HttpOnly para permitir que React env�e el token de solicitud en memoria.

Login tiene rate limiting por direcci�n remota. Identity mantiene lockout despu�s de intentos fallidos. No se registran contrase�as, hashes, security stamps, cookies ni cadenas de conexi�n.

## Autorizaci�n y tenants

La API conserva la pol�tica `contracts.read` basada en `permission=contracts.read`. Los permisos no se agregan entre tenants. La resoluci�n de tenant mediante membres�a activa todav�a est� pendiente; no se conf�a en un tenant enviado libremente por el navegador ni se simula aislamiento de datos que a�n no existe.

## Secretos y bases

Est� prohibido guardar credenciales, tokens o cadenas de conexi�n reales en Git, `appsettings.json`, archivos `.env` versionados, fixtures, logs o documentaci�n. LegacySql conserva �nicamente acceso de lectura para el primer vertical.

## Entra External ID

Microsoft Entra External ID queda como alternativa OIDC postergada. Sus componentes de transformaci�n se conservan aislados como referencia hist�rica, pero no se registran como autenticaci�n productiva de esta versi�n.

## Migraciones

La primera migraci�n de Identity deber� ejecutarse mediante un procedimiento controlado sobre una base nueva y no productiva, con revisi�n del modelo, respaldo, aprobaci�n, evidencia de aplicaci�n y plan de reversa. Nunca se ejecutar� una migraci�n sobre la base Legacy.
