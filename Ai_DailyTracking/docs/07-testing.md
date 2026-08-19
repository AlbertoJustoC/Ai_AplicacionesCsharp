# 07 - Guia De Pruebas

## Estado Actual
El proyecto todavia no tiene un proyecto de pruebas automatizadas. Este documento define la estrategia recomendada para cuando se agregue.

## Estrategia General
- Priorizar pruebas unitarias sobre `Application/Services` y `Domain/Models`, donde vive la logica de negocio (validaciones, calculo de nombres de archivo, filtrado/agrupacion para el panel grafico).
- La capa `UI/Forms` y `UI/Controls` (WinForms) es dificil de probar de forma unitaria; para esta capa se recomienda una combinacion de:
  - Pruebas manuales guiadas (smoke tests) antes de cada entrega.
  - Extraer la logica no visual de los formularios hacia servicios/helpers que si sean testeables (ver `.github/copilot-instructions.md`, seccion de refactorizacion).

## Estructura Sugerida Del Proyecto De Pruebas
```text
tests/
`-- Ai_DailyTracking.Tests/
    |-- Application/
    |   `-- ProjectWorkspaceServiceTests.cs
    |-- Domain/
    |   `-- TrackingEntryTests.cs
    |-- Infrastructure/
    |   `-- JsonFileStoreTests.cs
    `-- Shared/
        `-- ProjectFileNameHelperTests.cs
```
Framework recomendado: xUnit (o MSTest/NUnit si el equipo ya tiene preferencia establecida en otros proyectos).

## Candidatos Prioritarios A Cubrir Primero
- `ProjectWorkspaceService`: creacion de proyectos, creacion/eliminacion de registros, carga del ultimo proyecto abierto.
- `ProjectFileNameHelper`: sanitizacion de nombres de archivo a partir del nombre de proyecto.
- `JsonFileStore`: lectura/escritura basica, manejo de archivo inexistente.
- Logica de filtrado/agrupacion usada por `TrackingChartForm` (idealmente extraida a un metodo o clase testeable en `Application/Services` en lugar de vivir solo en el formulario).

## Pruebas Manuales Minimas (Mientras No Haya Automatizacion)
Tras cualquier cambio relevante:
1. `dotnet build -c Debug` sin errores ni advertencias nuevas.
2. Arrancar el `.exe` compilado y confirmar que el proceso permanece activo y respondiendo (no se cierra solo por una excepcion no controlada).
3. Crear un proyecto de prueba, agregar un registro, editar un campo de cada tipo (texto, texto largo, lista, fecha) y confirmar el autoguardado.
4. Eliminar un registro y confirmar que la app no se rompe si el proyecto se queda sin registros.
5. Abrir el panel grafico, cambiar el "Agrupar por", aplicar filtros y el rango de fechas, y confirmar que el grafico se actualiza sin errores.
6. Cerrar y volver a abrir la aplicacion para confirmar que reabre el ultimo proyecto usado sin errores (caso historico de bug, ver `docs/08-troubleshooting.md`).
