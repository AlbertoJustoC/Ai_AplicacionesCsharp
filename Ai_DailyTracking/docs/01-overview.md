# 01 - Overview

## Objetivo Funcional
Construir una aplicacion de seguimiento diario que permita capturar datos de forma estructurada y trabajar con archivos Excel para consulta, actualizacion o exportacion.

## Alcance Inicial
- Captura de informacion diaria desde interfaz de escritorio.
- Validacion basica de entradas.
- Persistencia automatica por proyecto en JSON.
- Recuperacion automatica del ultimo proyecto abierto.
- Preparacion para crecimiento modular y futura exportacion a XLSX.

## Casos De Uso Principales
1. Registrar datos diarios.
2. Editar y validar registros existentes con autoguardado.
3. Crear proyectos nuevos y retomar el ultimo proyecto usado.
4. Consultar historico de registros.
5. Ajustar el formulario modificando un esquema externo de campos.

## Flujo General
1. Usuario abre la aplicacion.
2. La app intenta cargar el ultimo proyecto desde preferencias locales.
3. Si no hay ultimo proyecto, se solicita crear uno nuevo.
4. Usuario interactua con el formulario de tracking.
5. Cada cambio se serializa automaticamente al archivo JSON del proyecto.
6. La UI muestra historico, estado del registro y campos pendientes.

## Principios De Evolucion
- Evitar crecimiento monolitico de formularios.
- Separar claramente orquestacion de UI y negocio.
- Favorecer componentes reutilizables para import/export y validacion.
