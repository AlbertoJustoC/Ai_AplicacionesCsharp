# 05 - Infrastructure

## Responsabilidad
La infraestructura implementa detalles tecnicos de:
- Lectura/escritura de archivos.
- Integracion futura con Excel.
- Manejo de rutas, serializacion y persistencia.

## Estructura Recomendada
- `Infrastructure/` con componentes especializados.

Ejemplos esperados:
- `JsonFileStore`
- `TrackingProjectRepository`
- `AppPreferencesRepository`
- `TrackingSchemaRepository`
- `TrackingReportPdfExporter`

## Implementacion Actual
- `JsonFileStore` centraliza serializacion JSON con `System.Text.Json`.
- `TrackingProjectRepository` guarda cada proyecto en un archivo independiente dentro de una carpeta configurable (`AppPreferences.ProjectsFolderPath`, si el usuario elige una carpeta compartida) o, por defecto, en `%AppData%\AiDailyTracking\Projects`. Tambien escribe copias de seguridad con marca de fecha/hora en `%AppData%\AiDailyTracking\backup` al cerrar la aplicacion, y borra el archivo de un proyecto (`Delete`) cuando se elimina desde la UI.
- `AppPreferencesRepository` recuerda el ultimo proyecto abierto y la carpeta de proyectos elegida por el usuario.
- `TrackingSchemaRepository` crea y carga `tracking-schema.json` para que el formulario sea configurable sin recompilar. Al cargar, sanea los campos `EditableOption`: si `Options`/`LastValue` tienen valores que no estan en los defaults hardcodeados de `CreateDefaultSchema` (residuo de versiones donde estos valores se guardaban en el esquema global en vez de por proyecto), los restaura a los defaults y persiste el cambio.

## Carpeta De Proyectos Compartida
- El boton "Carpeta de proyectos" permite elegir una carpeta (p. ej. una ruta de red) donde se guardaran los proyectos, para que varias personas accedan a los mismos datos.
- Al cambiarla, `TrackingProjectRepository.CompareWithFolder` compara los proyectos cargados en la aplicacion (carpeta actual) contra los que existen en la carpeta candidata (por `ProjectId`). El selector de proyectos solo muestra los proyectos que existen fisicamente en la carpeta seleccionada: los proyectos que solo existian en la aplicacion (carpeta anterior) NO se copian a la carpeta nueva ni aparecen en el selector; los que existen en ambos lados con contenido distinto se reportan como conflicto.
- Si no hay ningun conflicto ni diferencia, no se muestra ningun mensaje y el cambio de carpeta se aplica directamente.
- Si hay un conflicto (mismo proyecto con version distinta en la aplicacion y en la carpeta destino), se muestra el dialogo `ProjectVersionConflictDialog` con dos opciones visuales (fecha de cada version) y un boton de cancelar; la opcion "Usar version de la carpeta destino" es la recomendada/por defecto (foco inicial), pero el usuario puede elegir la version de la aplicacion o cancelar el cambio de carpeta.
- Si se acepta, `TrackingProjectRepository.ApplyFolderChange` escribe en la carpeta nueva la version elegida de cada proyecto y actualiza la preferencia de carpeta (`AppPreferences.ProjectsFolderPath`), que se recuerda de forma fiable entre sesiones y se vuelve a abrir siempre al iniciar la aplicacion.
- En cualquier caso (aceptado o cancelado, y ya con las decisiones de conflicto tomadas), `TrackingProjectRepository.WriteFolderChangeLog` añade un bloque con fecha/hora al archivo historico `cambio-carpeta-historial.log`, guardado en la carpeta destino (no en la carpeta de origen), con el detalle de las diferencias encontradas y la decision tomada; el archivo se conserva y se va ampliando con cada cambio de carpeta.

## Copias De Seguridad Al Cerrar
- Cada vez que se cierra la aplicacion, se guarda una copia del proyecto activo en `%AppData%\AiDailyTracking\backup` con nombre `{proyecto}-{yyyyMMdd-HHmmss}.json`, para poder recuperarlo si el archivo oficial del proyecto se pierde o corrompe.

## Exportacion De Informe PDF
- `TrackingReportPdfExporter` genera el PDF (via `System.Drawing.Printing.PrintDocument` y `PrintDialog`, sin dependencias NuGet) del boton "Crear PDF (A3)" en `TrackingReportForm`; ver [ADR 0004](adr/0004-informe-pdf-con-printdocument.md).
- Recibe directamente las entradas ya filtradas por `TrackingReportForm` (mismos filtros de campo/opciones/rango de fechas que la tabla en pantalla) y el `TrackingFormSchema` del proyecto, para imprimir una columna por cada campo de la ficha (no un subconjunto fijo). El grafico (calculado con `TrackingChartSeriesBuilder` y volcado a `Bitmap` con `TrackingLineChartPanel.DrawToBitmap`) se imprime en su propia pagina final, escalado para ajustarse a la pagina conservando su proporcion ancho x alto.
- El papel se fuerza a A3 apaisado: `PrintDocument.DefaultPageSettings.Landscape = true` y `PaperSize` tomado de `PrinterSettings.PaperSizes` (kind `A3`) si el driver lo reporta, o una medida de reserva de 297x420mm en caso contrario.

## Reglas
- No mezclar reglas de negocio con codigo de infraestructura.
- Aislar dependencias externas (librerias de Excel) detras de interfaces cuando aplique.
- Manejar errores tecnicos con mensajes claros para capas superiores.

## Consideraciones De Excel
- Definir formato de columnas y encabezados en un solo lugar.
- Validar hojas y rangos esperados antes de procesar.
- Preservar consistencia de tipos (fecha, numerico, texto).

## Decision De Persistencia
Para la primera version se usa JSON por proyecto en lugar de XLSX o base de datos porque:
- simplifica el autoguardado inmediato,
- permite inspeccion manual rapida,
- evita bloqueo de archivos Excel abiertos,
- deja abierta una futura exportacion/importacion sin acoplar la UI al formato final.
