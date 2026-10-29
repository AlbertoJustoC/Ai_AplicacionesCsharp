# Ai_DailyTracking

Aplicacion de escritorio (WinForms, .NET 8) para el seguimiento diario de tareas/incidencias de proyectos de ingenieria: cada entrada del dia se registra en una ficha con campos configurables, y queda disponible en un historico filtrable, con graficos e informes en PDF.

## Enfoque
- Una persona (o un equipo, si se comparte la carpeta de proyectos) lleva un "diario" por proyecto: cada entrada es un evento/tarea del dia con disciplina, area, paquete, estado, actividad, notas, etc.
- Los campos del formulario NO estan escritos a fuego en el codigo: se generan a partir de un esquema (`tracking-schema.json`) editable, por lo que se pueden anadir/quitar campos (listas cerradas, texto largo, fechas...) sin recompilar.
- Los cambios de cada registro se guardan al pulsar "Guardar registro" o al confirmar el guardado al cambiar de registro, proyecto o cerrar. Cada guardado crea un archivo de cambio único; la app fusiona los cambios disponibles y reconstruye un JSON de trabajo local.

## Que hace hoy
- Abre automaticamente el ultimo proyecto usado; si no existe ninguno, pide crear uno nuevo.
- Formulario de ficha diaria generado desde el esquema (texto, listas, fechas, texto largo con tamano ajustable).
- Historico de entradas con columnas ID/Modificado (fecha de "Ultimo cambio")/Disciplina/Area/Paquete/Estado, ordenable.
- Gestion de proyectos: crear, eliminar, elegir "carpeta de proyectos" compartida (para que varias personas lean/escriban desde la misma ruta de red) y ver/editar los usuarios del proyecto.
- "Crear Informe": ventana con hasta 3 filtros por campo combinables (AND), filtro por rango de fechas, grafico de barras y exportacion a PDF (tamano A3, con grafico acumulado y linea de tiempo al final).
- El PDF conserva el orden de los registros visible en la tabla al pulsar "Crear PDF (A3)", tras ordenar por cualquier columna en sentido ascendente o descendente.
- Grafico dedicado (`TrackingChartForm`) de entradas acumuladas por dia, agrupado por el campo que se elija.
- Exportacion de una ficha concreta a Markdown y vinculo opcional a una "Carpeta Documentacion" por entrada.
- Rutas compartidas adaptadas al perfil de Windows actual; si no existe el destino, se puede localizar desde una carpeta raíz y recordar la correspondencia en ese PC.
- Copia de seguridad automatica del proyecto activo al cerrar la aplicacion.

## Persistencia (configuración y JSON de trabajo en `%AppData%`)
- Proyectos: `%AppData%\AiDailyTracking\Projects\<nombre-proyecto>-<id>.json` (o la carpeta sincronizada elegida con "Carpeta de proyectos"). El JSON original es la base histórica; los cambios compartidos están en `.AiDailyTrackingChanges\` y el JSON de trabajo de cada PC en `%AppData%\AiDailyTracking\Working\`.
- Copias de seguridad: `%AppData%\AiDailyTracking\backup\`
- Preferencias de la app: `%AppData%\AiDailyTracking\app-preferences.json`
- Esquema del formulario: `%AppData%\AiDailyTracking\tracking-schema.json`

## Compilar y ejecutar
```powershell
dotnet build -c Release
dotnet run
```

Para la guia completa de que copiar en un equipo nuevo, donde instalarlo y como usar la app paso a paso, ver [MANUAL_USUARIO.md](MANUAL_USUARIO.md).

Documentacion tecnica ampliada (arquitectura, UI, servicios, convenciones): [docs/README.md](docs/README.md).
