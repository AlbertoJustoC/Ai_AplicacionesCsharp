# Manual de usuario - Ai_NewNotificationBlink

Guia para llevar esta aplicacion a un ordenador nuevo: que copiar, donde, como compilarla (el `.exe` no viaja en GitHub) y como se usa dia a dia.

## 1. Que copiar de GitHub a tu ordenador
GitHub solo guarda el **codigo fuente**, no el ejecutable (las carpetas `bin/` y `obj/` estan excluidas via `.gitignore`). Necesitas:

1. Clonar o descargar todo el repositorio `Ai_AplicacionesCsharp` (o al menos la carpeta `Ai_NewNotificationBlink/` completa).
2. Tener instalado el [.NET SDK 8.0](https://dotnet.microsoft.com/download/dotnet/8.0) o superior (no basta con el "runtime", hace falta el SDK para compilar).

No hace falta copiar nada mas.

## 2. Donde recomiendo copiar la carpeta
- Copia la carpeta del repositorio a una ruta local sencilla, por ejemplo `C:\Apps\Ai_AplicacionesCsharp\`.
- Es una app de un solo usuario/equipo por instalacion (no comparte datos entre ordenadores), asi que basta con tenerla en local en cada maquina donde quieras el aviso de notificaciones.

## 3. Compilar la aplicacion (generar el .exe)
Abre una terminal (PowerShell) en la carpeta `Ai_NewNotificationBlink` (dentro del repo clonado) y ejecuta:

```powershell
cd C:\Apps\Ai_AplicacionesCsharp\Ai_NewNotificationBlink
dotnet build -c Release
```

Al terminar sin errores, el ejecutable queda en:

```
Ai_NewNotificationBlink\bin\Release\net8.0-windows\Ai_NewNotificationBlink.exe
```

(Copia la carpeta **completa** `net8.0-windows`, no solo el `.exe` suelto: ahi estan tambien las `.dll` necesarias).

## 4. Llevar el ejecutable a su carpeta destino
1. Copia el contenido completo de `bin\Release\net8.0-windows\` a la carpeta donde quieras que viva la app instalada, por ejemplo `C:\Apps\Ai_NewNotificationBlink\`.
2. Crea un acceso directo a `Ai_NewNotificationBlink.exe` en el escritorio o en el menu de inicio.
3. Para actualizar mas adelante: repite `dotnet build -c Release` y vuelve a copiar la carpeta `net8.0-windows` completa sobre la carpeta destino (la configuracion no se pierde: vive en `%AppData%`, no en la carpeta del ejecutable). Cierra la app antes de sobrescribir los archivos.

## 5. Primer arranque
1. Ejecuta `Ai_NewNotificationBlink.exe`. No aparece ninguna ventana: la app se queda como un icono en la bandeja del sistema (junto al reloj, puede estar oculto en la flechita "mostrar iconos ocultos").
2. Al arrancar, la monitorizacion se activa automaticamente.
3. (Opcional) Haz clic derecho en el icono -> "Configuracion..." -> marca "Iniciar automaticamente con Windows" si quieres que arranque solo al encender el ordenador.

## 6. Uso paso a paso

### Activar/desactivar el aviso
- Clic derecho en el icono de la bandeja -> "Iniciar monitorizacion" / "Detener monitorizacion" (el texto cambia segun el estado).
- Doble clic sobre el icono tambien alterna el estado.

### Cuando llega una notificacion nueva
- Si Outlook esta abierto, su ventana parpadea en la barra de tareas.
- Ademas (si esta activado) aparece una pequena ventana auxiliar parpadeante en la esquina de la pantalla, para que el aviso sea visible aunque Outlook este minimizado.

### Ajustar el comportamiento
Clic derecho en el icono -> "Configuracion...":
- **Intervalo de sondeo (segundos)**: cada cuanto se revisa si hay notificaciones nuevas.
- **Parpadear la ventana de Outlook**: activar/desactivar ese aviso.
- **Mostrar ventana auxiliar de aviso**: activar/desactivar la ventanita emergente.
- **Reproducir sonido**: activar/desactivar el sonido de aviso.
- **Iniciar automaticamente con Windows**: registra la app para arrancar sola al iniciar sesion.
- **Nombres del icono de notificaciones**: texto(s) que identifican el icono de notificaciones a vigilar en la barra de tareas, separados por `;`. Normalmente no hace falta tocarlo; solo si la deteccion no funciona en tu configuracion regional de Windows.

### Si el aviso no se dispara (diagnostico)
1. Clic derecho en el icono -> "Volcar arbol de UI Automation (diagnostico)" o "Volcar arbol de la barra de tareas (diagnostico)".
2. Esto genera `%AppData%\AiNewNotificationBlink\automation-tree-dump.txt` con la estructura que la app esta viendo en pantalla, util para ajustar los "Nombres del icono de notificaciones" en Configuracion.

### Probar la ventana auxiliar sin esperar una notificacion real
- Clic derecho en el icono -> "Probar ventana auxiliar".

## 7. Donde queda mi configuracion
- Configuracion: `%AppData%\AiNewNotificationBlink\settings.json`
- Volcado de diagnostico: `%AppData%\AiNewNotificationBlink\automation-tree-dump.txt`

Si necesitas mover la app a otro ordenador conservando tu configuracion, copia el archivo `settings.json` anterior al mismo lugar en el ordenador nuevo.
