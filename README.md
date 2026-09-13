# uCredit

uCredit es la nueva generación de ProLeaseNet. Se construye como una aplicación nueva y modular que conservará inicialmente la compatibilidad con la base de datos SQL Server Legacy.

## Estado

Fase de fundación y documentación. El primer vertical aprobado es **Consulta de contratos de sólo lectura**.

## Objetivo

- modernizar la experiencia y arquitectura;
- rescatar reglas de negocio del Legacy;
- permitir migración gradual por módulos;
- evitar afectaciones a clientes actuales;
- mejorar seguridad, pruebas, observabilidad y despliegue.

## Tecnología propuesta

- .NET 10 LTS, C# y ASP.NET Core.
- Monolito modular.
- REST/JSON y OpenAPI.
- React + TypeScript para el frontend.
- SQL Server Legacy mediante Dapper.
- xUnit y pruebas de integración/caracterización.

## Documentación

- [Objetivo del proyecto](docs/PROJECT.md)
- [Arquitectura](docs/ARCHITECTURE.md)
- [Compatibilidad con la BD](docs/DATABASE-COMPATIBILITY.md)
- [Plan de migración](docs/MIGRATION-PLAN.md)
- [Consulta de contratos](docs/modules/CONTRACT-QUERY.md)
- [Decisiones arquitectónicas](docs/decisions/)

## Regla principal

La base Legacy es un contrato de compatibilidad. No debe modificarse sin autorización explícita, análisis de impacto y plan de reversa.

## Próximo hito

Validar el esqueleto con .NET SDK 10 y conectar el primer vertical a una copia local/QA mediante una identidad SQL de sólo lectura. Consulta [Compilar y ejecutar](docs/BUILD-AND-RUN.md).

## Estructura implementada

- API ASP.NET Core.
- Módulos Contracts y Branding.
- Adaptador `LegacySql` con Dapper.
- Frontend React/TypeScript.
- Tema dinámico por cliente con fallback uCredit.
- Pruebas unitarias y de arquitectura iniciales.
