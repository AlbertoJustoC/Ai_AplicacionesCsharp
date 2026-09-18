# Ai_NewNotificationBlink

Aplicacion de bandeja del sistema (WinForms, .NET 8) que avisa visualmente cuando llega una notificacion nueva en Windows, pensada sobre todo para no perderse avisos de correo de Outlook cuando la ventana no esta en primer plano.

## Enfoque
- No usa la API de Microsoft Graph (evita tener que registrar una app en Azure y autenticarse). En su lugar, vigila mediante UI Automation el icono/contador de notificaciones de la barra de tareas de Windows.
- Al detectar que el contador de notificaciones sube, hace parpadear la ventana de Outlook y/o muestra una pequena ventana auxiliar en una esquina de la pantalla que tambien parpadea, para que el aviso sea dificil de pasar por alto.
- Vive en la bandeja del sistema (system tray): no tiene ventana principal, se controla con el menu contextual del icono.
- Limitacion conocida y aceptada: al vigilar el contador agregado de notificaciones de la barra de tareas, reacciona a notificaciones de **cualquier** aplicacion, no solo de Outlook.

## Que hace hoy
- Sondea (por defecto cada 5+ segundos, configurable) el icono de notificaciones de la barra de tareas mediante UI Automation.
- Cuando el contador sube, dispara el aviso: parpadeo de la ventana de Outlook (si esta abierta) y/o ventana auxiliar propia en la esquina inferior derecha, tambien parpadeante.
- Menu de la bandeja: iniciar/detener monitorizacion, probar la ventana auxiliar, volcar el arbol de UI Automation o de la barra de tareas (diagnostico, para ajustar la deteccion si no funciona en tu equipo), configuracion, salir.
- Configuracion (menu "Configuracion..."): intervalo de sondeo, activar/desactivar parpadeo de Outlook, activar/desactivar ventana auxiliar, activar/desactivar sonido, iniciar automaticamente con Windows, nombres del icono de notificaciones a vigilar.
- Arranque con Windows opcional (se registra en el registro de usuario, sin necesidad de permisos de administrador).

## Persistencia (todo en `%AppData%`, no requiere instalador)
- Configuracion: `%AppData%\AiNewNotificationBlink\settings.json`
- Volcado de diagnostico de UI Automation: `%AppData%\AiNewNotificationBlink\automation-tree-dump.txt`

## Compilar y ejecutar
```powershell
dotnet build -c Release
dotnet run
```

Para la guia completa de que copiar en un equipo nuevo, donde instalarlo y como usar la app paso a paso, ver [MANUAL_USUARIO.md](MANUAL_USUARIO.md).
