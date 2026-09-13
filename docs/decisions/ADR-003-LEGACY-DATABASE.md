# ADR-003: Base Legacy como contrato de compatibilidad

- Estado: Propuesto para aprobación
- Fecha: 2026-09-13

## Contexto

Los clientes, procedimientos, reportes e integraciones dependen del esquema actual. Una migración de datos inicial elevaría el riesgo y bloquearía la convivencia.

## Decisión

uCredit consumirá inicialmente la BD existente mediante una capa anticorrupción. No habrá migraciones automáticas ni cambios sin aprobación.

## Consecuencias

- permite migración incremental;
- mantiene restricciones del esquema actual temporalmente;
- exige mapear nombres y reglas Legacy;
- cualquier escritura futura requiere coordinación de concurrencia y propiedad.

