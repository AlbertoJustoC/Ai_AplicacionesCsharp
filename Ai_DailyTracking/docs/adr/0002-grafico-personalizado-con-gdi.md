# 0002 - Grafico Personalizado Con GDI+

## Estado
Aceptado.

## Contexto
Se necesitaba un panel grafico visual (tipo dashboard) para representar los registros de seguimiento agrupados por campos de lista cerrada (disciplina, estado, prioridad, origen), con filtros incluyendo rango de fechas. Las opciones consideradas fueron: una libreria de charting de terceros (por ejemplo `System.Windows.Forms.DataVisualization` o un paquete NuGet equivalente para WinForms en .NET 8) o dibujar el grafico manualmente con GDI+ (`System.Drawing.Graphics`).

## Decision
Se implemento `TrackingBarChartPanel` (en `UI/Controls/`), un `Panel` que dibuja un grafico de barras propio en su evento `OnPaint`, sin dependencias de NuGet adicionales.

## Motivos
- Evita agregar y mantener una dependencia externa de charting (con su propio ciclo de versiones y posibles incompatibilidades con .NET 8/WinForms).
- El grafico requerido es simple (barras con conteo por categoria); GDI+ es mas que suficiente y totalmente controlable en estilo/colores.
- Reduce el riesgo de bugs de layout como los ya vividos con controles complejos de terceros (ver ADR 0003).

## Consecuencias
- Actualizacion (2026-08-07): la necesidad de un grafico de lineas anticipada mas abajo ya se dio: `TrackingChartForm` ahora usa `TrackingLineChartPanel` (mismo enfoque GDI+ sin dependencias nuevas) para mostrar un grafico de lineas por fecha (X = fechas de los registros, Y = conteo por dia), con una linea por cada opcion seleccionada de un campo de lista elegido por el usuario. `TrackingBarChartPanel` se mantiene en el repositorio mientras no tenga un consumidor.
- Si en el futuro se necesitan tipos de grafico mas complejos (series apiladas, tooltips interactivos), se debera evaluar de nuevo una libreria de charting o ampliar los paneles existentes.
- El panel grafico (`TrackingChartForm`) sigue basandose en cualquier campo de tipo `Option`/`EditableOption` del esquema, no en columnas fijas, para mantenerse alineado con la filosofia de esquema configurable del ADR 0001.
