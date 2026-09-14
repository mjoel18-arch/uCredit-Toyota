# ADR-006: Microsoft Entra External ID para identidad de uCredit

- Estado: Aprobado para primera etapa
- Fecha: 2026-09-13

## Decisión

uCredit usará un tenant externo central de Microsoft Entra External ID. La autenticación será browser-delegated: React funcionará como SPA y ASP.NET Core .NET 10 como API protegida.

La SPA y la API tendrán registros independientes. External ID autentica al usuario, pero uCredit conserva la responsabilidad de resolver y validar el cliente, las membresías, los permisos y el branding.

El permiso productivo `contracts.read` permanece como política de autorización de uCredit. Un app role `contracts.read` recibido en `roles` puede transformarse en el claim interno `permission=contracts.read` mediante un componente aislado.

## Límites de confianza

- Entra no determina directamente qué base Legacy puede consultar un usuario.
- uCredit resolverá y validará el cliente antes de usar sus permisos o branding.
- Nunca se confiará en un tenant enviado libremente por el navegador.
- uCredit no almacenará contraseñas.
- No se aceptarán headers, query strings ni cookies como sustitutos de permisos.

## Evolución

La integración queda aislada detrás de configuración y componentes OIDC/JWT para permitir sustituir Entra por otro proveedor OIDC en el futuro. Esta etapa no crea recursos en Azure ni implementa resolución de cliente o MSAL en React.