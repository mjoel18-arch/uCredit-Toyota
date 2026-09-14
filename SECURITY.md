# Seguridad e identidad

## Microsoft Entra External ID

uCredit adopta un tenant externo central de Microsoft Entra External ID con autenticación browser-delegated. React será la SPA y ASP.NET Core .NET 10 será la API protegida; ambos se registrarán de forma independiente.

External ID autentica al usuario. La API valida la firma, issuer, audience y expiración del JWT mediante HTTPS metadata. Los tokens no se guardan ni se imprimen en logs.

La API mantiene la política `contracts.read` y no acepta un header, query string o cookie como permiso. El app role `contracts.read` recibido en `roles` se transforma de forma aislada a `permission=contracts.read`; si el permiso ya existe, no se duplica.

Entra no decide qué base Legacy puede consultar un usuario. uCredit debe resolver y validar el cliente, membresías, permisos y branding. Nunca debe confiarse en un tenant enviado libremente por el navegador. uCredit no almacena contraseñas.

La configuración se proporciona por `Authentication__Authority`, `Authentication__Audience` y `Authentication__ClientId` mediante secretos de usuario o configuración segura del ambiente. No se deben guardar valores reales en Git.

La integración está aislada para permitir otro proveedor OIDC futuro. Esta etapa no crea recursos en Azure, no implementa MSAL en React y no resuelve todavía el cliente.