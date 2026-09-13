# ADR-001: Monolito modular

- Estado: Propuesto para aprobación
- Fecha: 2026-09-13

## Contexto

ProLeaseNet contiene reglas altamente relacionadas y una BD compartida con integridad implícita. Separar de inmediato en microservicios añadiría transacciones distribuidas, despliegues múltiples y duplicación de datos antes de entender los límites.

## Decisión

Construir uCredit como monolito modular: un despliegue principal con límites verificables entre módulos.

## Consecuencias

- operación y transacciones más simples;
- extracción futura posible si existe evidencia;
- requiere pruebas de arquitectura para evitar acoplamiento;
- los módulos no equivalen automáticamente a proyectos o servicios.

