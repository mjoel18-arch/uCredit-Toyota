# ADR-007: ASP.NET Core Identity para la primera versi�n

- Estado: Aprobado
- Fecha: 2026-09-15

## Decisi�n

Usar ASP.NET Core Identity con Entity Framework Core �nicamente sobre una base nueva de seguridad. La autenticaci�n productiva ser� mediante cookie HttpOnly, Secure fuera de pruebas y SameSite=Lax, con lockout, antiforgery y rate limiting.

La conexi�n se recibe exclusivamente como `IdentitySql__ConnectionString`. La base Legacy contin�a aislada, con Dapper y conexi�n de lectura. No se ejecutan migraciones autom�ticas ni se crean usuarios al iniciar.

## Autorizaci�n y evoluci�n

La pol�tica `contracts.read` contin�a requiriendo el claim sem�ntico `permission=contracts.read`. El tenant se selecciona contra membres�as activas y la cookie firmada se regenera con permisos exclusivamente de la membres�a seleccionada. `X-Tenant-Code` no es autoridad. Esta selecci�n todav�a no filtra contratos ni elige conexiones Legacy.

La frontera de autenticaci�n permite retomar OIDC/Entra External ID en una fase futura sin que los m�dulos funcionales dependan del proveedor.

## Revalidación del principal

La cookie se revalida en cada solicitud autenticada contra Identity. Se rechaza y elimina cuando el usuario, tenant o membresía ya no están activos; los permisos se sustituyen y la cookie se renueva cuando cambian. Esto tiene un costo de consulta por solicitud y podrá optimizarse posteriormente con caché invalidable.