# 0004 - Informe PDF Con PrintDocument (Sin Dependencias)

## Estado
Aceptado.

## Contexto
Se pidio poder generar un informe con todo el historico de un proyecto y el grafico al final, en formato PDF. Las opciones consideradas fueron: agregar una libreria de generacion de PDF (por ejemplo QuestPDF) o usar la API de impresion nativa de WinForms (`System.Drawing.Printing.PrintDocument`) junto con la impresora virtual "Microsoft Print to PDF" incluida en Windows.

## Decision
Se implemento la exportacion usando `PrintDocument` + `PrintDialog` (`Infrastructure/TrackingReportPdfExporter.cs`), sin agregar ninguna dependencia NuGet. El usuario elige el destino (tipicamente "Microsoft Print to PDF") en el dialogo de impresion estandar de Windows.

## Motivos
- El proyecto no tiene ninguna dependencia externa hoy (`Ai_DailyTracking.csproj` esta limpio); esto mantiene esa base.
- Sigue la misma filosofia que la ADR 0002 (preferir GDI+/APIs nativas antes que sumar librerias de terceros cuando el resultado requerido es alcanzable con ellas).
- El usuario mantiene control total del destino (puede elegir "Microsoft Print to PDF", otra impresora fisica, o cancelar), a traves del dialogo de impresion estandar de Windows.

## Consecuencias
- El dialogo/nombre "Guardar salida de impresion como" para elegir la ruta del PDF lo controla el propio driver de "Microsoft Print to PDF", no la aplicacion; no se puede omitir ni prellenar una ruta de archivo de forma programatica.
- La paginacion de la tabla del historico se calcula a mano (filas por pagina segun la altura disponible) dentro de `TrackingReportPdfExporter`; el grafico se imprime siempre en su propia pagina final, reutilizando `TrackingLineChartPanel` (volcado a `Bitmap` con `DrawToBitmap`) para no duplicar el dibujo del grafico.
- El informe usa un grafico "por defecto" (primer campo de lista del esquema, todas sus opciones, sin filtro de fechas) calculado en `Application/Services/DailyTrackingReportBuilder.cs`; no reutiliza la seleccion de series/fechas que el usuario haya elegido en `TrackingChartForm` porque son instancias independientes.
- Si en el futuro se necesita mas control de maquetacion (tablas con estilos, paginacion mas compleja, encabezados repetidos automaticamente) se debera reevaluar sumar una libreria como QuestPDF.
- Actualizacion (2026-08-07): el boton "Crear informe" se redefinio para NO generar PDF por ahora; abre `TrackingReportForm`, una ventana con filtros (al estilo `TrackingChartForm`) que muestra en pantalla una tabla con todos los campos de la ficha para los registros filtrados. `DailyTrackingReportBuilder` y `TrackingReportPdfExporter` quedan sin usar en el repositorio, conservados por si se retoma la exportacion a PDF sobre esa misma base.
- Actualizacion (2026-08-07 - se retoma la exportacion a PDF): `TrackingReportForm` ahora tiene un boton "Crear PDF (A3)" que reutiliza los mismos filtros aplicados en pantalla (campo, opciones marcadas -incluyendo "(Vacio)"- y rango de fechas opcional) tanto para la tabla como para el grafico del PDF, en vez del historico completo sin filtrar de la version anterior. Se reescribio `TrackingReportPdfExporter` para: imprimir una columna por cada campo del esquema (no solo ID/Fecha/Actividad/Estado), usar papel A3 apaisado (`PrintDocument.DefaultPageSettings.Landscape = true` + `PaperSize` A3, con una version de reserva de 297x420mm si el driver no reporta A3), y escalar el grafico a la pagina conservando su proporcion ancho x alto. Se extrajo el calculo de series acumulativas (antes duplicado en `TrackingChartForm` y en `DailyTrackingReportBuilder`) a una clase compartida `Application/Services/TrackingChartSeriesBuilder.cs`, usada ahora por `TrackingChartForm` y por el nuevo flujo de PDF. `DailyTrackingReportBuilder`, `DailyTrackingReportData` y `DailyTrackingReportRow` quedan completamente huerfanos (ya no los referencia `TrackingReportPdfExporter`); se conservan en el repositorio a la espera de confirmacion para eliminarlos.

