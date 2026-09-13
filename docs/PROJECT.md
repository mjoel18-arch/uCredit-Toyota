# Proyecto uCredit

## Visión

Reemplazar gradualmente ProLeaseNet con una plataforma moderna, mantenible y segura, conservando el comportamiento funcional y la compatibilidad necesarios para la continuidad de los clientes.

## Problema

ProLeaseNet utiliza VB.NET, ASP.NET WebForms y .NET Framework 4.5. El sistema contiene cientos de pantallas, 661 tablas, 727 procedimientos y reglas distribuidas entre UI, bibliotecas y SQL. Una conversión automática reproduciría el acoplamiento existente y podría perder reglas de negocio.

## Alcance estratégico

- Ingeniería inversa de procesos Legacy.
- Arquitectura nueva en .NET 10/C#.
- Migración incremental por capacidades.
- Convivencia temporal de ambos sistemas.
- Compatibilidad con SQL Server actual.
- Pruebas de equivalencia funcional.
- Sustitución gradual de reportes e integraciones.

## Fuera de alcance inicial

- Migración completa de todos los módulos.
- Rediseño masivo de la BD.
- Microservicios.
- Reemplazo simultáneo de todas las integraciones.
- Escritura desde el primer vertical.

## Primer producto mínimo

Consulta de contratos de sólo lectura con búsquedas por contrato, persona, RFC, nombre, VIN y solicitud; filtros por operación/estatus; paginación; permisos; detalle inicial y equivalencia con el Legacy.

## Principios

1. Continuidad del cliente antes que modernización tecnológica.
2. Conocimiento y comportamiento antes que traducción de código.
3. Seguridad y autorización desde el diseño.
4. Cambios pequeños, medibles y reversibles.
5. Evidencia mediante pruebas y trazabilidad.

## Actores

- Negocio/Producto: valida reglas y diferencias.
- Desarrollo: implementa módulos y pruebas.
- QA: valida equivalencia y aceptación.
- TI: identidad, infraestructura, pipelines y operación.
- Datos/DBA: aprueba cambios SQL e índices.
- Cliente: valida procesos y resultados cuando aplique.

## Indicadores iniciales

- 100 % de SQL nuevo parametrizado.
- 0 migraciones automáticas sobre Legacy.
- equivalencia de campos críticos en casos de prueba acordados;
- despliegues sólo mediante pipeline;
- trazabilidad de cada endpoint a su caso de uso y objetos SQL;
- tiempos de respuesta objetivo definidos con volúmenes reales.

## Riesgos principales

- relaciones de datos implícitas;
- reglas ocultas en WebForms y procedimientos;
- diferencias de parametrización por cliente;
- consultas Legacy con concatenación SQL;
- falta de pruebas históricas;
- integraciones no presentes en el código analizado;
- convivencia de identidades y permisos.

