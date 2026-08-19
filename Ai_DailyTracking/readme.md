# Ai_DailyTracking

Aplicacion WinForms para seguimiento diario de tareas e inputs de proyectos con enfoque practico para equipos de ingenieria.

## Lo que hace hoy
- Abre automaticamente el ultimo proyecto usado.
- Si no existe un proyecto previo, solicita crear uno nuevo al iniciar.
- Guarda cada cambio al instante en un archivo JSON externo por proyecto.
- Mantiene un historico de registros del proyecto.
- Genera el formulario desde un esquema configurable con campos de texto, listas cerradas y fechas.

## Persistencia
- Proyectos: `%AppData%\AiDailyTracking\Projects\<nombre-proyecto>-<id>.json`
- Preferencias: `%AppData%\AiDailyTracking\app-preferences.json`
- Esquema del formulario: `%AppData%\AiDailyTracking\tracking-schema.json`

## Ejecucion
```powershell
dotnet build
dotnet run
```

## Nota sobre la pestaña Tracking del Excel
El formulario queda preparado para adaptarse a la estructura exacta de la hoja `Tracking` editando `tracking-schema.json`. En este cambio se deja un esquema inicial razonable porque el archivo Excel adjunto no estuvo accesible fisicamente desde el entorno de ejecucion.
