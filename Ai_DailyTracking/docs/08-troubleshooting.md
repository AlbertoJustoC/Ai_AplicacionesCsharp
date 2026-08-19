# 08 - Guia De Errores Y Troubleshooting

Este documento recopila problemas reales encontrados durante el desarrollo, su causa raiz y la solucion aplicada, para evitar repetirlos.

## La aplicacion se cierra sola al abrir (excepcion de `SplitterDistance`)
- **Sintoma**: la app se cerraba inmediatamente al arrancar, sin mostrar ventana.
- **Causa**: `SplitContainer` tenia `Panel1MinSize`, `Panel2MinSize` y `SplitterDistance` asignados en el inicializador de objeto, antes de que el control tuviera un ancho real. WinForms lanzaba `ArgumentOutOfRangeException`/`InvalidOperationException` con el mensaje "SplitterDistance debe estar entre Panel1MinSize y Ancho - Panel2MinSize".
- **Solucion**: se elimino `SplitContainer` y se reemplazo por un `TableLayoutPanel` de dos columnas (ver [adr/0003-tablelayoutpanel-en-lugar-de-splitcontainer.md](adr/0003-tablelayoutpanel-en-lugar-de-splitcontainer.md)).
- **Leccion**: evitar fijar propiedades de tamano/posicion que dependen del ancho/alto real de un control antes de que el control tenga layout asignado (constructor/inicializador es demasiado pronto). Si se necesita, hacerlo en `HandleCreated` o `Resize`.

## `dotnet run` procesa pero no ejecuta / el `.exe` generado no arranca
- **Sintoma**: `dotnet run` compilaba sin errores pero no se veia ninguna ventana, y ejecutar el `.exe` generado tampoco abria nada visible.
- **Causa**: relacionado con el mismo bug de `SplitContainer` de arriba; la excepcion ocurria durante la construccion del formulario principal, antes de que `Application.Run` pudiera mostrar la ventana, y al no haber un manejador de excepciones no controladas visible, el proceso terminaba en silencio.
- **Solucion**: la misma que el punto anterior. Adicionalmente, para diagnosticar este tipo de casos, lanzar el `.exe` y verificar con `Get-Process ... | Select-Object Responding` si el proceso sigue vivo tras unos segundos.

## La app abre la primera vez pero no la segunda (stack overflow / cuelgue)
- **Sintoma**: tras generar el primer registro y cerrar la app, volver a abrirla no mostraba ninguna ventana la segunda vez.
- **Causa**: recursion infinita entre `EntriesListView_SelectedIndexChanged` y `SelectCurrentEntryInList()`. Este ultimo asignaba `ListViewItem.Selected = true` sin condicion, lo que podia volver a disparar `SelectedIndexChanged` aunque el valor no cambiara realmente; el manejador del evento no estaba suprimido en ese punto y volvia a llamar a `LoadEntry`, que a su vez volvia a llamar a `SelectCurrentEntryInList()`, formando un bucle infinito hasta el stack overflow.
- **Solucion**: se envolvio la asignacion de seleccion en `SelectCurrentEntryInList()` guardando/activando/restaurando la bandera `_suppressEvents`, de forma que cualquier evento `SelectedIndexChanged` reentrante se corte de inmediato.
- **Leccion**: cualquier codigo que fije la seleccion de un `ListView` de forma programatica debe protegerse con una bandera de supresion, porque `ListViewItem.Selected` puede volver a disparar el evento incluso sin un cambio real de estado.

## Colision de nombres entre `System.Windows.Forms.Application` y el namespace propio `Application`
- **Sintoma**: error de compilacion al llamar a `Application.Run(...)` en `Program.cs`.
- **Causa**: el proyecto tiene su propio namespace `Ai_DailyTracking.Application.Services`, que colisiona con la clase `System.Windows.Forms.Application`.
- **Solucion**: usar el nombre completamente calificado `System.Windows.Forms.Application.Run(...)` en `Program.cs`.
- **Leccion**: al nombrar una carpeta/namespace `Application` (convencion comun en arquitecturas por capas), revisar si el framework usado (WinForms en este caso) ya tiene una clase con ese mismo nombre en el nivel raiz, y calificar explicitamente donde haga falta.

## Como Reportar Un Nuevo Problema Aqui
Al resolver un bug no trivial, agregar una seccion con: sintoma observado, causa raiz confirmada y solucion aplicada. Evitar registrar suposiciones no verificadas.
