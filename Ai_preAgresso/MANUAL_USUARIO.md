# Manual de usuario - Ai_preAgresso

Guia para llevar esta aplicacion a un ordenador nuevo: que copiar, donde, como compilarla (el `.exe` no viaja en GitHub) y como se usa dia a dia.

## 1. Que copiar de GitHub a tu ordenador
GitHub solo guarda el **codigo fuente**, no el ejecutable (las carpetas `bin/` y `obj/` estan excluidas via `.gitignore`). Necesitas:

1. Clonar o descargar todo el repositorio `Ai_AplicacionesCsharp` (o al menos la carpeta `Ai_preAgresso/` completa).
2. Tener instalado el [.NET SDK 8.0](https://dotnet.microsoft.com/download/dotnet/8.0) o superior (no basta con el "runtime", hace falta el SDK para compilar).

La primera compilacion descarga automaticamente el paquete NuGet `ClosedXML` (necesita acceso a internet la primera vez).

## 2. Donde recomiendo copiar la carpeta
- Copia la carpeta del repositorio a una ruta local sencilla, por ejemplo `C:\Apps\Ai_AplicacionesCsharp\`.
- Los datos (horas registradas) NO tienen por que estar en esa misma ruta: se guardan donde tu elijas (ver punto 5), asi que la carpeta del codigo/ejecutable puede vivir en local sin problema.

## 3. Compilar la aplicacion (generar el .exe)
Abre una terminal (PowerShell) en la carpeta `Ai_preAgresso` (dentro del repo clonado) y ejecuta:

```powershell
cd C:\Apps\Ai_AplicacionesCsharp\Ai_preAgresso
dotnet build -c Release
```

Al terminar sin errores, el ejecutable queda en:

```
Ai_preAgresso\bin\Release\net8.0-windows\Ai_preAgresso.exe
```

(Copia la carpeta **completa** `net8.0-windows`, no solo el `.exe` suelto: ahi estan tambien las `.dll` necesarias, incluida `ClosedXML.dll`).

## 4. Llevar el ejecutable a su carpeta destino
1. Copia el contenido completo de `bin\Release\net8.0-windows\` a la carpeta donde quieras que viva la app instalada, por ejemplo `C:\Apps\Ai_preAgresso\`.
2. Crea un acceso directo a `Ai_preAgresso.exe` en el escritorio o en el menu de inicio.
3. Para actualizar mas adelante: repite `dotnet build -c Release` y vuelve a copiar la carpeta `net8.0-windows` completa sobre la carpeta destino (tus datos no se pierden: viven donde hayas guardado tu archivo `.json`, no en la carpeta del ejecutable). Cierra la app antes de sobrescribir los archivos.

## 5. Primer arranque
1. Ejecuta `Ai_preAgresso.exe`.
2. Si es la primera vez, la app usa por defecto `%AppData%\AiPreAgresso\entries.json` (se crea automaticamente).
3. Si quieres usar un archivo distinto (por ejemplo, para empezar un año nuevo o compartir el archivo por red), usa el boton "Archivo de proyecto..." para elegir o crear uno en la ruta que prefieras.

## 6. Uso paso a paso

### Rellenar el parte semanal
1. Elige el año/semana (o usa "Semana actual", o haz clic en el numero de semana del calendario para seleccionarla entera).
2. En la rejilla, cada fila representa una tarea de un dia: elige el dia, el tipo de jornada (Normal/Fiesta/Vacaciones/Baja), proyecto, actividad, descripcion y horas.
3. Proyecto y Actividad tienen autocompletado con los valores usados anteriormente (escribe para filtrar o haz clic en la flecha para ver todos).
4. Las horas admiten hasta 2 decimales; si un valor no es valido, la app avisa antes de guardar.
5. Guarda con el boton correspondiente. Si intentas cambiar de semana con cambios sin guardar, la app pregunta si quieres guardar, descartar o cancelar.

### Consultar el listado completo
1. Abre "Listado".
2. Filtra por Proyecto, Actividad, Descripcion (texto libre), Tipo, Año, Mes y/o Semana. Los filtros son en cascada: al elegir uno, los demas desplegables se acotan a las opciones compatibles.
3. Revisa el subtotal de horas de las filas visibles (segun los filtros aplicados).

### Ver el grafico de horas (quesitos)
1. Desde el Listado, abre "Grafico de horas (quesitos)".
2. Elige "Agrupar por": Proyecto, Actividad o Tipo.
3. El grafico usa exactamente las filas que esten visibles segun los filtros del listado.

### Exportar/importar a Excel
- Usa las opciones de exportar/importar Excel para volcar los datos actuales a un `.xlsx` compatible con las plantillas Agresso, o para cargar datos desde un Excel existente.

### Cambiar de archivo de datos
- Boton "Archivo de proyecto...": permite abrir un archivo `.json` existente o crear uno nuevo en la ruta que indiques. Util para separar datos por año, por cliente, o para compartir el archivo por una carpeta de red.

## 7. Donde quedan mis datos
- Archivo de datos activo: el que hayas elegido con "Archivo de proyecto..." (por defecto, `%AppData%\AiPreAgresso\entries.json`).
- Preferencias (recuerda cual fue el ultimo archivo de datos usado): `%AppData%\AiPreAgresso\preferences.json`.

Si necesitas mover la app a otro ordenador conservando tus datos, copia tu archivo `.json` de datos al ordenador nuevo y, la primera vez, selecciona esa ruta con "Archivo de proyecto...".
