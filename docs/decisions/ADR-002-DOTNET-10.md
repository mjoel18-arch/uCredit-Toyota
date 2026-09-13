# ADR-002: .NET 10 LTS y C#

- Estado: Propuesto para aprobación
- Fecha: 2026-09-13

## Contexto

El Legacy usa .NET Framework 4.5, VB.NET y WebForms. .NET 8 concluye soporte en noviembre de 2026; .NET 10 LTS permanece soportado hasta noviembre de 2028.

## Decisión

Usar C#, ASP.NET Core y .NET 10 LTS para nuevos componentes web y backend.

## Consecuencias

- se abandona `System.Web` y WebForms;
- se requiere capacitación de VB.NET a C#;
- no se intenta compilar las páginas Legacy en el proyecto nuevo;
- se adopta un ciclo de actualización periódico de .NET.

## Referencias

- https://learn.microsoft.com/en-us/lifecycle/products/microsoft-net-and-net-core
- https://learn.microsoft.com/en-us/dotnet/core/releases-and-support

