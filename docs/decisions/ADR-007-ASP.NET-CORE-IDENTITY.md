# ADR-007: ASP.NET Core Identity para la primera versi�n

- Estado: Aprobado
- Fecha: 2026-09-15

## Decisi�n

Usar ASP.NET Core Identity con Entity Framework Core �nicamente sobre una base nueva de seguridad. La autenticaci�n productiva ser� mediante cookie HttpOnly, Secure fuera de pruebas y SameSite=Lax, con lockout, antiforgery y rate limiting.

La conexi�n se recibe exclusivamente como `IdentitySql__ConnectionString`. La base Legacy contin�a aislada, con Dapper y conexi�n de lectura. No se ejecutan migraciones autom�ticas ni se crean usuarios al iniciar.

## Autorizaci�n y evoluci�n

La pol�tica `contracts.read` contin�a requiriendo el claim sem�ntico `permission=contracts.read`. La resoluci�n y el aislamiento real de tenant quedan pendientes; no se agregan permisos cross-tenant mientras no exista esa validaci�n.

La frontera de autenticaci�n permite retomar OIDC/Entra External ID en una fase futura sin que los m�dulos funcionales dependan del proveedor.
