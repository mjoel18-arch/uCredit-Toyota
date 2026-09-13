# ADR-005: Identidad visual dinámica por cliente

- Estado: Aprobado
- Fecha: 2026-09-13

## Contexto

uCredit será contratado por distintas empresas y debe adaptarse a su identidad visual sin crear ramas, builds o versiones independientes.

## Decisión

Implementar temas dinámicos por cliente mediante un contrato de Branding y variables CSS. Ningún componente puede contener colores o logotipos específicos de un cliente.

El frontend aprobado es React con TypeScript.

## Capacidades

- logotipo y nombre comercial;
- colores primario, secundario, acento y navegación;
- superficie, texto, tipografía y modo claro/oscuro;
- tema predeterminado uCredit;
- validación y fallback;
- resolución del cliente desacoplada de la UI.

## Seguridad y accesibilidad

- sólo valores validados;
- archivos de marca desde ubicaciones autorizadas;
- contraste WCAG por validar antes de publicar;
- configuración funcional separada de la identidad visual;
- el encabezado temporal `X-Tenant-Code` del esqueleto no es el mecanismo productivo de identidad.

## Consecuencias

- un mismo despliegue sirve a varios clientes;
- los componentes deben usar variables semánticas;
- se necesitará almacenamiento y administración de temas;
- la resolución productiva del tenant dependerá de identidad/host y debe definirse con TI.
