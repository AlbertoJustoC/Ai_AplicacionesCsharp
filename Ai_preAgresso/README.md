# Ai_preAgresso

Aplicacion de escritorio (WinForms, .NET 8) para registrar el parte de horas semanal al estilo Agresso: una rejilla por dia con proyecto/actividad/descripcion/horas, con listado, filtros, graficos y exportacion/importacion a Excel.

## Enfoque
- Pensada como sustituto rapido del parte de horas en Excel: misma terminologia (Proyecto/Actividad/Descripcion/Horas) pero con una rejilla semanal editable, autocompletado de valores anteriores y validacion de horas.
- Cada dia de la semana es una fila (o varias, si hay mas de una tarea ese dia); el tipo de jornada (Normal/Fiesta/Vacaciones/Baja) colorea la fila para verlo de un vistazo.
- Los datos se guardan en un unico archivo JSON (no hay base de datos), elegible por el usuario, para poder tener por ejemplo un archivo distinto por año o por cliente.

## Que hace hoy
- Rejilla semanal (`MainForm`): selector de semana con calendario (clic en el numero de semana selecciona la semana completa), columna "Dia" tipo "L 18", columna "Tipo" que colorea la fila.
- Autoguardado con confirmacion: si hay cambios sin guardar y cambias de semana/archivo, pregunta si quieres guardar, descartar o cancelar.
- Listado completo (`ListadoForm`): filtros combinables por Proyecto, Actividad, Descripcion (texto libre con autocompletado), Tipo, Año, Mes y Semana; los filtros son "en cascada" (elegir uno acota las opciones de los demas). Muestra el subtotal de horas filtradas.
- Grafico de quesitos (`HorasPieChartForm`) de horas agrupadas por Proyecto, Actividad o Tipo, respetando los filtros activos del listado.
- Exportacion/importacion a Excel (formato compatible con las plantillas Agresso) mediante ClosedXML.
- El archivo de datos a usar es elegible por el usuario (boton "Archivo de proyecto..."), no esta fijado a una unica ruta.

## Persistencia (todo en `%AppData%`, no requiere instalador)
- Archivo de datos por defecto: `%AppData%\AiPreAgresso\entries.json` (puedes elegir otra ruta/archivo desde la app).
- Preferencias (recuerda el ultimo archivo de datos usado): `%AppData%\AiPreAgresso\preferences.json`.

## Compilar y ejecutar
```powershell
dotnet build -c Release
dotnet run
```

Para la guia completa de que copiar en un equipo nuevo, donde instalarlo y como usar la app paso a paso, ver [MANUAL_USUARIO.md](MANUAL_USUARIO.md).
