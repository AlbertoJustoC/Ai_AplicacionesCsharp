# 06 - Convenciones

## Convenciones De Estructura
- Organizar por feature/responsabilidad, no por acumulacion de utilidades.
- Mantener una clase publica por archivo cuando sea practico.
- Evitar archivos gigantes; dividir por cohesion funcional.

## Convenciones De Nombres
- Clases: sustantivo claro + rol (Service, Helper, Model).
- Metodos: verbo + objetivo (ValidateDateRange, ExportDailyRows).
- Variables: nombres explicitos y sin abreviaturas ambiguas.

## Convenciones De Calidad
- Cambios pequenos e incrementales.
- Comentarios solo para logica no obvia.
- Evitar duplicacion de codigo mediante extraccion de metodos/helpers.

## Convenciones De Dependencias
- Preferir inyeccion por constructor.
- Minimizar dependencias estaticas ocultas.
- Encapsular librerias de terceros detras de adaptadores cuando sea util.

## Convenciones De Documentacion
- Si se agrega una nueva parte principal, crear o actualizar su documento en `docs/`.
- Mantener `docs/README.md` como indice oficial.
- Reflejar cambios de arquitectura en el mismo PR/commit donde se implementan.
