# ADR-006: Microsoft Entra External ID para identidad de uCredit

- Estado: Postergado
- Fecha original: 2026-09-13
- Actualizado: 2026-09-15

## Decisi�n hist�rica

Se hab�a considerado un tenant externo central de Microsoft Entra External ID con React como SPA y ASP.NET Core como API protegida. La decisi�n se conserva como antecedente, pero no aplica a la primera versi�n porque el equipo no dispone de permisos de Azure.

## Estado actual

La primera versi�n usa ASP.NET Core Identity con cookie segura y una base de seguridad independiente. La transformaci�n de app roles a permisos se conserva como frontera futura, no como configuraci�n activa.

## Evoluci�n

Podr� retomarse OIDC/Entra cuando exista proveedor aprobado, permisos de Azure y un plan de migraci�n de claims, sesiones, tenants y pruebas. Nunca se confiar� en un tenant enviado libremente por el navegador.
