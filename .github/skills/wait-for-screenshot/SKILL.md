---
name: wait-for-screenshot
description: 'Use when the user''s message refers to a screenshot/capture (e.g. "captura", "imagen", "screenshot", "print de pantalla", "mira la foto", "como se ve en la app") but no image is attached to that message. Also use as a reminder after any code change in this repo to keep every project markdown doc up to date.'
---

# Esperar captura adjunta antes de trabajar

## Cuando usar
- El usuario menciona una captura/imagen/pantallazo ("en la captura...", "mira la imagen...", "como se ve en pantalla...", "te adjunto una foto...") pero el mensaje actual NO trae ninguna imagen adjunta.
- No aplica si la imagen ya esta adjunta en el mismo mensaje (en ese caso, trabaja normalmente, no preguntes).

## Procedimiento
1. No empieces a investigar codigo ni a hacer cambios todavia.
2. Responde brevemente pidiendo que adjunte la captura mencionada (usa `vscode_askQuestions` si conviene, o simplemente contesta pidiendola).
3. Espera al siguiente mensaje del usuario con la imagen adjunta antes de proponer o aplicar ningun cambio.
4. Si el usuario insiste en continuar sin adjuntarla, sigue sus instrucciones explicitas, pero deja claro que estas asumiendo sin ver la captura.

## Regla adicional: mantener la documentacion al dia
Cada vez que se complete una actualizacion de codigo en cualquiera de los proyectos de este repositorio (`Ai_DailyTracking`, `Ai_NewNotificationBlink`, `Ai_preAgresso`), revisa y actualiza si hace falta:
- El `README.md` del proyecto afectado (explicacion/enfoque/funcionamiento).
- El `MANUAL_USUARIO.md` del proyecto afectado (pasos de compilacion, despliegue y uso), si el cambio afecta al flujo de uso.
- El `README.md` raiz del repositorio, si el cambio afecta a la descripcion general de alguna aplicacion.
- La documentacion tecnica en `Ai_DailyTracking/docs/` cuando el cambio sea en ese proyecto y afecte a la arquitectura, UI o servicios descritos alli.

No es necesario tocar archivos que no cambian de significado por la actualizacion (evita ediciones innecesarias).
