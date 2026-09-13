# AGENTS.md — Reglas de trabajo para uCredit

Este archivo aplica a todo el repositorio.

## Objetivo

Construir uCredit como reemplazo incremental de ProLeaseNet Legacy, utilizando tecnología moderna y conservando la compatibilidad operativa con la base de datos actual.

## Principio rector

> No migrar código. Migrar conocimiento y comportamiento, protegiendo la continuidad operativa de los clientes.

## Tecnología aprobada

- Backend: C# y ASP.NET Core sobre .NET 10 LTS.
- Arquitectura: monolito modular.
- API: REST/JSON con OpenAPI.
- Frontend aprobado: React y TypeScript.
- SQL Server Legacy: acceso mediante Dapper y `Microsoft.Data.SqlClient`.
- Pruebas: xUnit, integración SQL, arquitectura y caracterización.

## Reglas críticas de la base Legacy

1. No modificar tablas, columnas, vistas, procedimientos, funciones, índices, restricciones, usuarios ni permisos sin autorización explícita asociada a una HU.
2. No ejecutar migraciones automáticas sobre la base Legacy.
3. El primer vertical sólo puede usar una conexión de lectura.
4. Toda entrada SQL debe enviarse mediante parámetros tipados.
5. Se prohíbe concatenar entrada del usuario en SQL.
6. No agregar `NOLOCK` como solución general.
7. No crear índices sin evidencia de plan de ejecución, prueba y aprobación.
8. Nunca usar una base productiva para pruebas automatizadas.
9. No incluir datos de clientes en fixtures, repositorio, logs o documentación.

## Código Legacy

- El código VB.NET/WebForms es material de referencia, no dependencia del producto nuevo.
- No copiar bloques completos de VB o SQL sin documentar la regla que representan.
- Cada equivalencia debe registrar: pantalla, evento, clase, SQL/SP, tablas y comportamiento esperado.
- Si el comportamiento no es claro, detener la implementación y documentar la duda.
- No corregir ni modificar el repositorio Legacy como parte de uCredit salvo solicitud explícita.

## Dependencias permitidas

- `uCredit.Api` puede depender de módulos y de composición de infraestructura.
- Los módulos funcionales definen sus contratos de persistencia.
- `uCredit.Infrastructure.LegacySql` implementa esos contratos.
- Los módulos funcionales no pueden depender de `LegacySql`, Web, WebForms, `DataSet` o `DataTable`.
- `uCredit.Web` consume la API y nunca accede directamente a SQL Server.
- Ningún módulo debe acceder a tablas de otro módulo sin un contrato documentado.

## Convenciones de implementación

- Código y nombres técnicos en inglés; documentación funcional puede estar en español.
- Usar tipos de dominio y nombres comprensibles, no abreviaciones Legacy fuera del adaptador SQL.
- Dinero y tasas usan `decimal`, nunca `double`.
- Fechas deben distinguir fecha de negocio, fecha local y UTC.
- Propagar `CancellationToken` en las operaciones asíncronas.
- No bloquear llamadas asíncronas con `.Result` o `.Wait()`.
- Evitar abstracciones genéricas antes de tener dos usos reales.
- No introducir microservicios, colas o caché sin una necesidad documentada en ADR.

## API

- Rutas bajo `/api/v1`.
- JSON en camelCase.
- Fechas ISO 8601.
- Errores con `ProblemDetails`.
- Paginación en servidor.
- Ordenamiento mediante lista permitida, nunca nombres de columna libres.
- No devolver excepciones, SQL, rutas internas ni secretos.
- La autorización se valida siempre en el servidor.

## Seguridad

- No guardar secretos en Git, `appsettings.json`, archivos `.env` versionados ni documentación.
- Usar secretos de usuario localmente y almacén seguro en ambientes compartidos.
- Aplicar mínimo privilegio a identidad, base de datos y pipelines.
- No registrar tokens, contraseñas, cadenas de conexión, RFC completo ni datos personales innecesarios.
- Toda mutación requiere permiso, validación, transacción cuando corresponda y auditoría.
- Agregar pruebas negativas de autorización e inyección.

## Pruebas obligatorias

Cada HU debe incluir las pruebas pertinentes:

- unitarias para reglas puras;
- integración para SQL y adaptadores;
- contrato para endpoints;
- arquitectura para dependencias prohibidas;
- caracterización para comparar resultados con ProLeaseNet;
- autorización para perfiles permitidos y rechazados.

No se considera válido un mock como única prueba de una consulta Legacy.

## Flujo antes de implementar una HU

1. Leer la HU y criterios de aceptación.
2. Revisar documentación del módulo y ADR aplicables.
3. Trazar comportamiento Legacy.
4. Identificar objetos SQL y efectos secundarios.
5. Clasificar la operación como query o command.
6. Documentar diferencias deseadas.
7. Proponer solución y pruebas.
8. Solicitar decisión si existe ambigüedad funcional o impacto en BD.

## Definition of Done

- Compila sin errores ni advertencias nuevas aceptadas silenciosamente.
- Pruebas pertinentes aprobadas.
- SQL parametrizado y revisado.
- Autorización del lado servidor.
- Sin secretos ni datos productivos.
- Equivalencia Legacy validada o diferencia aprobada.
- Documentación actualizada.
- Pipeline aprobado.
- Evidencia de QA disponible.
- Plan de reversa para cambios operativos.

## Acciones que requieren autorización explícita

- Modificar cualquier objeto de la BD Legacy.
- Ejecutar SQL de escritura fuera de una BD local desechable.
- Cambiar el esquema de autenticación.
- Incorporar un servicio externo o enviar información fuera del entorno.
- Agregar un nuevo framework, ORM, bus, caché o plataforma de observabilidad.
- Publicar o desplegar a un ambiente compartido.
- Modificar pipelines productivos.
- Implementar una diferencia funcional respecto al Legacy.

## Primer vertical

Consulta de contratos de sólo lectura:

- `GET /api/v1/contracts`
- `GET /api/v1/contracts/{contractNumber}`
- catálogos de estatus y tipo de operación;
- permiso `contracts.read`, equivalente inicialmente al control Legacy 56;
- consultas parametrizadas y paginadas;
- comparación contra ProLeaseNet.

Alta, edición y cancelación quedan fuera de este vertical.
