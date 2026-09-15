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

La API conserva la pol�tica `contracts.read` basada en `permission=contracts.read`. El usuario selecciona un tenant mediante `POST /api/v1/auth/select-tenant`; la API valida usuario activo, tenant activo y membres�a activa, y emite una nueva cookie firmada con s�lo los permisos de esa membres�a. El tenant activo se obtiene exclusivamente de `tenant_id` y `tenant_code` en la cookie; `X-Tenant-Code` no es autoridad. La resoluci�n de tenant no filtra todav�a contratos ni selecciona conexiones Legacy.

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