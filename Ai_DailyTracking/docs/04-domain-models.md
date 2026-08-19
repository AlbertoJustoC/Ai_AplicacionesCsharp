# 04 - Domain Models

## Responsabilidad
La capa de dominio define las estructuras de datos y reglas centrales del problema.

## Estructura Recomendada
- `Domain/Models/` para entidades y DTOs de dominio.

## Modelos Implementados
- `TrackingProject`: proyecto con nombre, identificador, archivo de almacenamiento, lista de registros, lista de usuarios permitidos (`AllowedUserNames`, correo o usuario de Windows; vacia significa visible para todos) y valores aprendidos por campo de lista editable propios de este proyecto (`CustomFieldOptions`).
- `TrackingEntry`: registro individual con numero de registro (`EntryNumber`, visible solo en el historico), valores dinamicos por campo y marcas de tiempo.
- `TrackingFormSchema`: definicion de la hoja de captura.
- `TrackingFieldDefinition`: metadatos de cada input del formulario (etiqueta, tipo, opciones, si es requerido, si inicia una fila nueva en la ficha, si recuerda el ultimo valor seleccionado y colores por opcion).
- `TrackingFieldType`: tipos soportados (`Text`, `LongText`, `Option`, `Date`, `EditableOption`).
- `AppPreferences`: ultimo proyecto abierto.

## `TrackingFieldType.EditableOption`
- Combo editable: el usuario puede escribir un valor nuevo o elegir uno ya introducido antes.
- Al confirmar un valor nuevo que no existe todavia, se agrega a `TrackingProject.CustomFieldOptions` (clave del campo) y se persiste en el JSON de ese proyecto; queda disponible para el siguiente registro solo dentro de ese proyecto. Los proyectos nuevos arrancan solo con las opciones por defecto definidas en `TrackingFieldDefinition.Options` (`tracking-schema.json`), sin heredar valores agregados en otros proyectos. `Shared/Helpers/FieldOptionsHelper.GetOptions` combina ambas listas (defaults + aprendidos del proyecto) para poblar combos, graficos e informes.
- `DefaultToLastValue` (opcional, usado en Disciplina) hace que los registros nuevos empiecen con el ultimo valor seleccionado (`LastValue`, guardado en el esquema global), en vez de en blanco.

## Validacion De Fecha Minima
- `TrackingFieldDefinition.MinDateFieldKey` (opcional, usado en "Ultimo cambio" -> "Fecha") impide que un campo de tipo `Date` tenga un valor anterior al de otro campo de fecha referenciado por clave.
- Si el usuario elige una fecha anterior a la minima, la UI ajusta el valor automaticamente a la fecha minima permitida; el ajuste se re-evalua tambien si el campo de referencia cambia despues.

## Colores Por Opcion
- `TrackingFieldDefinition.OptionColors` mapea un valor de opcion a un color hexadecimal, usado para pintar el combo de Prioridad, Complejidad y Estado en la ficha.

## Principios
- Modelos pequenos, con nombres claros y semanticos.
- Validaciones de invariantes de dominio en puntos bien definidos.
- Evitar mezclar metadatos de UI dentro del dominio.

## Modelado Elegido
- Los valores de cada registro se guardan en un diccionario `clave -> valor` para que el formulario pueda adaptarse a cambios de la hoja `Tracking` sin reescribir el dominio.
- Las fechas del registro y del proyecto se conservan como marcas de tiempo locales para priorizar la operacion diaria del equipo.

## Versionado De Datos
Cuando cambie el formato de datos:
- Documentar impacto en import/export.
- Mantener compatibilidad cuando sea viable.
