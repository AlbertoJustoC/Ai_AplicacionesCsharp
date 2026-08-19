# 0003 - TableLayoutPanel En Lugar De SplitContainer

## Estado
Aceptado.

## Contexto
El area principal de `DailyTrackingForm` necesitaba dos regiones: un panel lateral con el historico de registros y un panel central con el formulario dinamico. La primera implementacion uso `SplitContainer` con `Panel1MinSize`, `Panel2MinSize` y `SplitterDistance` asignados en el inicializador de objeto, antes de que el control tuviera un ancho real asignado por el layout. Esto provocaba una excepcion (`SplitterDistance debe estar entre Panel1MinSize y Ancho - Panel2MinSize`) que impedia arrancar la aplicacion.

## Decision
Se elimino `SplitContainer` y se reemplazo por un `TableLayoutPanel` de dos columnas (una columna `Absolute` de ancho fijo para el historico, una columna `Percent` al 100% para el formulario).

## Motivos
- `SplitContainer` valida sus propiedades de tamano contra el ancho real del control en tiempo de ejecucion; asignarlas demasiado pronto (antes de `HandleCreated`/layout) es fragil y dependiente del orden de inicializacion.
- Un primer intento de mover solo `SplitterDistance` a un manejador de `HandleCreated` no fue suficiente: `Panel1MinSize`/`Panel2MinSize` seguian disparando la misma validacion interna.
- El caso de uso no requiere que el usuario redimensione el splitter manualmente, por lo que `TableLayoutPanel` cubre la necesidad real sin la fragilidad asociada.

## Consecuencias
- Si en el futuro se requiere que el usuario pueda arrastrar el divisor entre el historico y el formulario, habra que reintroducir un control tipo splitter, con cuidado de fijar sus propiedades de tamano solo despues de que el control tenga dimensiones reales (por ejemplo, en el evento `Resize` o `HandleCreated`, nunca en el inicializador).
- Leccion general documentada tambien en `docs/08-troubleshooting.md`: evitar fijar propiedades dependientes del tamano de un control WinForms antes de que tenga layout real.
