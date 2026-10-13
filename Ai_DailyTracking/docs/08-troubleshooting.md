# 08 - Guia De Errores Y Troubleshooting

Este documento recopila problemas reales encontrados durante el desarrollo, su causa raiz y la solucion aplicada, para evitar repetirlos.

## Rutas de SharePoint sincronizado en otro PC

- **Perfil de Windows**: si una ruta antigua empieza por `C:\Users\<usuario>\`, la aplicación sustituye ese prefijo por la carpeta real del perfil de este PC y comprueba que el destino exista. Las rutas nuevas dentro del perfil se guardan como `userprofile:ACCIONA S.A/...`. No se pregunta por una raíz al elegir una nueva carpeta o un MD.
- **Rutas `sharepoint:` existentes**: siguen siendo válidas. La raíz local configurada anteriormente se usa si existe; también se pueden resolver mediante una correspondencia local guardada. La configuración queda en `%APPDATA%\AiDailyTracking\app-preferences.json`, no en el JSON compartido.
- **Si la sustitución falla**: al abrir la carpeta, el MD o el proyecto, la aplicación pide elegir una carpeta raíz. Busca si esa carpeta o una de sus subcarpetas inmediatas tiene el mismo nombre que algún tramo de la ruta antigua. Reconstruye la ruta desde ese tramo y solo la acepta si el destino completo existe. Si hay varias coincidencias, el usuario elige. La correspondencia se recuerda en este PC para las demás rutas bajo el mismo tramo.
- **Límite**: si la biblioteca está en otra unidad o tiene nombres diferentes, sustituir el usuario no basta. Selecciona una raíz más cercana al archivo; no se deduce una ruta inexistente ni se cambia automáticamente el JSON compartido al localizarla.

## Fusión de proyectos en una carpeta sincronizada

- **Sin Entra**: cada equipo apunta a la misma carpeta de proyectos sincronizada. El JSON original de cada proyecto queda como base histórica. Cada guardado crea un archivo de cambio único en `.AiDailyTrackingChanges/<id-proyecto>/`. El JSON reconstruido para trabajar se guarda en `%APPDATA%\AiDailyTracking\Working\` de cada equipo; la primera edición guarda allí una copia del JSON original en `BaselineBackups`.
- **Cuándo se fusiona**: al abrir, al pulsar **Guardar registro**, al elegir Guardar al cambiar de registro o proyecto, al pulsar **Fusionar** y al cerrar. Solo se incorporan archivos que ya hayan llegado a este PC. El aviso indica cuántos cambios nuevos se incorporaron; no confirma que OneDrive ya los haya subido a la nube.
- **Usuarios activos**: cada sesión actualiza un archivo de presencia por minuto. La aplicación avisa cuando detecta otra sesión reciente en el proyecto. Un archivo de más de diez minutos se ignora; el aviso depende de que OneDrive haya sincronizado esos archivos.
- **«La operación de nube no es válida» al abrir un cambio**: el archivo aparece en la carpeta sincronizada, pero Windows/OneDrive no permite leerlo en este PC. En el Explorador, marca la carpeta `.AiDailyTrackingChanges` del proyecto como **Mantener siempre en este dispositivo** y espera a que termine la sincronización antes de reabrir el proyecto. Si persiste, comprueba que el archivo indicado se abre desde SharePoint web y revisa el estado de OneDrive. No borres ni ignores el archivo: contiene cambios que deben fusionarse.
- **Conflictos**: los campos distintos se combinan. Si dos usuarios cambian el mismo campo desde la misma versión, la aplicación conserva ambos cambios y pide elegir. También pide elegir si un borrado de registro compite con una edición aún no vista. Las rutas de Carpeta y MD eligen la revisión de ruta más reciente; las líneas de Notas se añaden sin preguntar. La elección del usuario se guarda como otro cambio.
- **Migración y orden**: los registros antiguos conservan primero su orden visible por número y su autor histórico queda como desconocido. Los nuevos se ordenan después por hora de creación; tras la fusión se renumeran todos. Los números provisionales duplicados no identifican registros: se usa `EntryId`.
- **Borrado de proyecto**: crea un marcador de eliminación sincronizable; el JSON original y el historial no se borran físicamente. Al recibir el marcador, el proyecto desaparece de la lista.
- **Límites**: la sincronización es eventual y los relojes de los PC pueden diferir. No se debe editar el JSON original con una versión antigua de la aplicación después de iniciar este modo; cada lote guarda una huella de esa base y la fusión se detiene si cambia. Los archivos MD siguen siendo archivos compartidos independientes; la fusión del proyecto no bloquea editores externos de MD.

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
