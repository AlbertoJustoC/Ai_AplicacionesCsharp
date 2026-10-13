# Manual de usuario - Ai_DailyTracking

Guia para llevar esta aplicacion a un ordenador nuevo: que copiar, donde, como compilarla (el `.exe` no viaja en GitHub) y como se usa dia a dia.

## 1. Que copiar de GitHub a tu ordenador
GitHub solo guarda el **codigo fuente**, no el ejecutable (las carpetas `bin/` y `obj/` estan excluidas via `.gitignore`). Necesitas:

1. Clonar o descargar todo el repositorio `Ai_AplicacionesCsharp` (o al menos la carpeta `Ai_DailyTracking/` completa).
2. Tener instalado el [.NET SDK 8.0](https://dotnet.microsoft.com/download/dotnet/8.0) o superior (no basta con el "runtime", hace falta el SDK para compilar).

No hace falta copiar nada mas: la app no depende de una base de datos ni de un instalador externo.

## 2. Donde recomiendo copiar la carpeta
- Copia la carpeta del repositorio a una ruta local sin caracteres raros ni rutas de red lentas, por ejemplo `C:\Apps\Ai_AplicacionesCsharp\`.
- **No hace falta** que la carpeta del proyecto este en red: los datos (proyectos, preferencias) se guardan aparte, en `%AppData%`, así que cada usuario puede tener su propia copia del codigo/ejecutable en local.
- Si varias personas van a compartir los mismos proyectos, eso se gestiona luego DESDE la app (boton "Carpeta de proyectos" -> elegir una carpeta de red), no copiando el codigo fuente a esa ruta.

## 3. Compilar la aplicacion (generar el .exe)
Abre una terminal (PowerShell) en la carpeta `Ai_DailyTracking` (dentro del repo clonado) y ejecuta:

```powershell
cd C:\Apps\Ai_AplicacionesCsharp\Ai_DailyTracking
dotnet build -c Release
```

Al terminar sin errores, el ejecutable queda en:

```
Ai_DailyTracking\bin\Release\net8.0-windows\Ai_DailyTracking.exe
```

(Esa misma carpeta contiene tambien las `.dll` necesarias; hay que copiar la carpeta **completa** `net8.0-windows`, no solo el `.exe` suelto).

## 4. Llevar el ejecutable a su carpeta destino
1. Copia el contenido completo de `bin\Release\net8.0-windows\` a la carpeta donde quieras que viva la app instalada, por ejemplo `C:\Apps\Ai_DailyTracking\`.
2. Crea un acceso directo a `Ai_DailyTracking.exe` en el escritorio o en el menu de inicio si quieres lanzarla facilmente.
3. Si mas adelante actualizas el codigo, repite el `dotnet build -c Release` y vuelve a copiar la carpeta `net8.0-windows` completa sobre la carpeta destino (no borra tus datos: estos viven en `%AppData%`, no en la carpeta del ejecutable).

## 5. Primer arranque
1. Ejecuta `Ai_DailyTracking.exe`.
2. Como no hay ningun proyecto todavia, la app pedira crear uno nuevo: escribe un nombre de proyecto.
3. La app crea el archivo del proyecto en `%AppData%\AiDailyTracking\Projects\` y lo abre automaticamente.

## 6. Uso paso a paso

### Registrar una entrada del dia
1. Rellena los campos de la ficha (Disciplina, Area, Paquete, Estado, Actividad, Notas, etc.).
2. Cuando cambies un campo, se ilumina **Guardar registro**. Púlsalo para grabar el cambio con autor, fecha y hora. Si cambias de registro, proyecto o cierras con cambios pendientes, la aplicación pregunta si quieres guardar, descartar o cancelar.
3. Los cuadros de texto largo (Actividad, Notas) se pueden arrastrar desde el borde inferior para hacerlos mas grandes.

### Fusionar cambios de otros usuarios
- Al abrir un proyecto, la aplicación avisa si detecta otros usuarios activos en los archivos de presencia ya sincronizados. Este aviso es informativo y no bloquea la edición. Si la sincronización se retrasa, el aviso también puede retrasarse.
- La aplicación incorpora los cambios que ya estén sincronizados al abrir, guardar un registro y cerrar. También puedes pulsar **Fusionar**.
- Si dos personas cambian el mismo campo de un registro, se muestran ambos valores y autores para elegir. Las Notas se reúnen como líneas añadidas: borrar o reescribir una línea anterior no elimina su versión histórica. Para Carpeta y ruta MD se conserva el cambio de ruta con fecha posterior.
- "Guardado localmente" confirma la escritura en este PC, no que OneDrive haya terminado de sincronizar. Los cambios que lleguen más tarde se incorporarán en la siguiente fusión.

### Consultar el historico
- La lista de la izquierda/abajo muestra ID, Fecha, Disciplina, Area, Paquete y Estado de todas las entradas del proyecto.
- Haz clic en las cabeceras de columna para ordenar.

### Cambiar de proyecto o crear uno nuevo
- Usa el selector de proyectos de la cabecera para cambiar entre proyectos ya creados.
- Boton "Nuevo proyecto" para crear otro proyecto distinto.
- Boton "Usuarios del proyecto" para ver/editar quien participa en el proyecto activo.

### Compartir proyectos entre varios ordenadores/usuarios
1. Pulsa el boton "Carpeta de proyectos" (arriba a la derecha).
2. Elige una carpeta compartida (por ejemplo, una ruta de red accesible por todo el equipo).
3. A partir de ese momento la app lee y guarda los proyectos en esa carpeta. Cada persona debe repetir este paso una vez, apuntando a la misma ruta compartida.

Las rutas guardadas bajo `C:\Users\<usuario>` se adaptan automáticamente al perfil del PC actual. Si no existe la ruta resultante, pulsa la ruta de la carpeta o del MD y elige una carpeta raíz que contenga un tramo de la ruta antigua. La aplicación comprobará la coincidencia y recordará esa correspondencia en este equipo.

### Carpeta de documentacion de una entrada
- Boton "Carpeta Documentacion": permite vincular una carpeta de disco con la documentacion de esa entrada/tarea concreta (por ejemplo, planos o correos relacionados). Clic derecho sobre la ruta mostrada para quitarla.

### Crear un informe en PDF
1. Abre "Crear Informe".
2. Configura hasta 3 filtros por campo (se combinan con Y logico) y, si quieres, un rango de fechas.
3. Revisa la tabla de resultados y el resumen.
4. Pulsa "Crear PDF (A3)" para generar el informe con grafico incluido.

### Ver el grafico de evolucion
- Abre el grafico de seguimiento para ver las entradas acumuladas por dia, agrupadas por el campo que elijas (por ejemplo, Estado o Disciplina).

### Exportar una ficha a Markdown
- Desde la ficha de una entrada, usa la opcion de exportar a Markdown para generar un archivo `.md` con el contenido de esa entrada (util para pegar en otras herramientas).

## 7. Donde quedan mis datos
- Proyectos: `%AppData%\AiDailyTracking\Projects\` (o la carpeta compartida que hayas elegido).
- Copias de seguridad automaticas (al cerrar la app): `%AppData%\AiDailyTracking\backup\`.
- Preferencias (ultimo proyecto abierto, carpeta de proyectos elegida, etc.): `%AppData%\AiDailyTracking\app-preferences.json`.
- Esquema de campos del formulario: `%AppData%\AiDailyTracking\tracking-schema.json`.

Si necesitas mover la app a otro ordenador conservando tus datos, copia la carpeta `%AppData%\AiDailyTracking\` completa al mismo lugar en el ordenador nuevo (o usa "Carpeta de proyectos" apuntando a una ubicacion compartida).
