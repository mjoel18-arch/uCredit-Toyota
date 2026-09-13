# Plan de migración de ProLeaseNet a uCredit

## Estrategia

Migración incremental tipo sustitución por capacidades. ProLeaseNet continúa operando mientras uCredit reemplaza módulos validados.

## Fases

### 0. Descubrimiento

- inventario técnico;
- cruce código/BD;
- mapa funcional;
- riesgos y arquitectura.

### 1. Fundación

- repositorio, documentación y ADR;
- solución compilable;
- pipeline;
- secretos y ambientes;
- autenticación/autorización base;
- observabilidad.

### 2. Primer vertical: Consulta de contratos

- catálogos;
- búsqueda paginada;
- detalle de sólo lectura;
- permisos;
- pruebas de caracterización;
- piloto controlado.

### 3. Contratación

- alta y modificación;
- productos y esquemas;
- amortización;
- scoring y autorizaciones;
- coexistencia de escritura.

### 4. Operación financiera

- desembolsos;
- movimientos/exigibilidades;
- pagos y cancelaciones;
- cobranza y mora;
- contabilidad.

### 5. Módulos especializados

- seguros;
- terminaciones;
- reestructuras y diferimientos;
- líneas de crédito;
- facturación;
- reportes e integraciones.

### 6. Retiro

- congelar módulos Legacy reemplazados;
- monitorear periodo acordado;
- retirar rutas, permisos y servidores;
- conservar evidencias y archivo histórico.

## Criterios para migrar un módulo

- propietario funcional definido;
- flujo Legacy documentado;
- objetos SQL identificados;
- datos/configuración por cliente conocidos;
- pruebas de caracterización aprobadas;
- plan de despliegue y reversa;
- monitoreo y soporte preparados.

## Convivencia

- identidad común o federada;
- enlaces/ruteo por módulo;
- no compartir memoria de sesión;
- evitar escrituras duplicadas;
- definir sistema propietario de cada operación;
- auditoría correlacionada.

## Estrategia de corte

1. Liberar a usuarios internos/QA.
2. Ejecutar comparación paralela de consultas.
3. Habilitar piloto a un grupo controlado.
4. Medir resultados y rendimiento.
5. Ampliar gradualmente.
6. Mantener reversa de navegación al Legacy durante estabilización.

## Riesgos y mitigación

| Riesgo | Mitigación |
|---|---|
| Regla oculta | Caracterización y validación funcional |
| Diferencia por cliente | Configuración inventariada y pruebas por perfil |
| Escritura concurrente | Propietario único por comando e idempotencia |
| Rendimiento | Datos representativos y planes SQL |
| Acceso excesivo | Mínimo privilegio y autorización en API |
| Dependencia externa | Adaptadores, timeouts y observabilidad |

