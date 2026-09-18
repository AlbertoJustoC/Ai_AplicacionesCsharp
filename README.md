# Ai_AplicacionesCsharp

Repositorio con varias aplicaciones de escritorio para Windows (WinForms, .NET 8), cada una en su propia carpeta e independiente de las demas (no hay un `.sln` comun, cada proyecto se compila por separado).

Los ejecutables compilados (carpetas `bin/`/`obj/`) **no se suben a GitHub** (ver `.gitignore`), asi que cada aplicacion debe compilarse localmente antes de poder usarse. Cada carpeta de proyecto incluye un `MANUAL_USUARIO.md` con los pasos exactos para hacerlo.

## Aplicaciones

| Proyecto | Que hace | Documentacion |
|---|---|---|
| [Ai_DailyTracking](Ai_DailyTracking/) | Seguimiento diario de tareas/incidencias por proyecto (fichas con campos configurables, historico filtrable, graficos, informes en PDF, exportacion a Markdown). | [README](Ai_DailyTracking/readme.md) · [Manual de usuario](Ai_DailyTracking/MANUAL_USUARIO.md) · [Docs tecnicas](Ai_DailyTracking/docs/README.md) |
| [Ai_NewNotificationBlink](Ai_NewNotificationBlink/) | Aplicacion de bandeja del sistema que avisa (parpadeo de ventana/overlay) cuando llegan notificaciones nuevas, pensada sobre todo para avisos de Outlook. | [README](Ai_NewNotificationBlink/README.md) · [Manual de usuario](Ai_NewNotificationBlink/MANUAL_USUARIO.md) |
| [Ai_preAgresso](Ai_preAgresso/) | Registro semanal de horas al estilo Agresso (rejilla por dia/proyecto/actividad), con listado filtrable, graficos y exportacion/importacion a Excel. | [README](Ai_preAgresso/README.md) · [Manual de usuario](Ai_preAgresso/MANUAL_USUARIO.md) |

## Requisitos comunes
- Windows 10/11.
- [.NET SDK 8.0](https://dotnet.microsoft.com/download/dotnet/8.0) o superior.

## Compilar cualquier proyecto
Desde la raiz del repositorio, situate en la carpeta del proyecto y compila en modo Release:

```powershell
cd Ai_DailyTracking   # o Ai_NewNotificationBlink / Ai_preAgresso
dotnet build -c Release
```

El ejecutable queda en `<Proyecto>/bin/Release/net8.0-windows/<Proyecto>.exe`. Cada `MANUAL_USUARIO.md` detalla que archivos copiar a la carpeta de destino en tu equipo.
