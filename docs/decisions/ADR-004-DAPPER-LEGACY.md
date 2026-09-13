# ADR-004: Dapper para acceso a SQL Legacy

- Estado: Propuesto para aprobación
- Fecha: 2026-09-13

## Contexto

La BD tiene 661 tablas y sólo 26 llaves foráneas declaradas. Un modelo EF Core completo inferiría relaciones incompletas y sería difícil de controlar.

## Decisión

Usar Dapper y `Microsoft.Data.SqlClient` para consultas/comandos Legacy. EF Core se reservará para estructuras nuevas justificadas.

## Consecuencias

- SQL explícito y control de rendimiento;
- mapeo manual y pruebas de integración obligatorias;
- parámetros tipados obligatorios;
- no se reutilizan las cadenas SQL concatenadas del Legacy.

