# 03 - Application Services

## Responsabilidad
Los servicios de aplicacion implementan casos de uso y coordinan:
- Validaciones de negocio.
- Transformacion de datos entre UI y dominio.
- Operaciones de infraestructura (archivos/Excel).

## Estructura Recomendada
- `Application/Services/` con clases enfocadas por caso de uso.

Servicio implementado actualmente:
- `ProjectWorkspaceService`
- `DailyTrackingReportBuilder` (estatico): construye datos de informe (filas formateadas + grafico por defecto) con un formato de fila fijo (ID/fecha/actividad/estado). No esta conectado a ningun boton/formulario ni lo usa `TrackingReportPdfExporter` (que ahora imprime directamente `TrackingProject.Entries` filtradas con todas las columnas del esquema); junto con `DailyTrackingReportData`/`DailyTrackingReportRow` queda totalmente huerfano, conservado a la espera de confirmacion para eliminarlo.
- `TrackingChartSeriesBuilder` (estatico): calcula las series de linea acumulativas (una por opcion con al menos un registro, mas una linea "Total" con la suma diaria de las demas) a partir de una lista de entradas, el campo de fecha y el campo de lista elegidos. Tambien centraliza `EmptyValueOption` ("(Vacio)") y `GetDisplayValue`. Lo usan tanto `TrackingChartForm` (panel grafico) como `TrackingReportForm` (grafico del PDF), para no duplicar esta logica.

## Responsabilidades Actuales De `ProjectWorkspaceService`
- Cargar el esquema de tracking.
- Crear proyectos nuevos.
- Recuperar el ultimo proyecto abierto.
- Abrir proyectos existentes.
- Crear registros nuevos dentro de un proyecto (asignando el siguiente numero de registro y precargando los campos marcados como `DefaultToLastValue`); el registro solo queda en memoria hasta que se guarda explicitamente.
- Eliminar registros existentes de un proyecto (solo en memoria hasta que se guarda explicitamente).
- Recordar valores de campos de lista editable (`RememberFieldValue`): un valor nuevo se guarda en `TrackingProject.CustomFieldOptions` y persiste el proyecto (queda disponible solo en ese proyecto; uno nuevo arranca solo con las opciones por defecto del esquema); el ultimo valor seleccionado (`LastValue`, campos con `DefaultToLastValue`) se guarda en el esquema global. Solo persiste lo que realmente cambio.
- Olvidar un valor de un campo de lista editable (`ForgetFieldValue`): quita el valor de `TrackingProject.CustomFieldOptions` y/o de las opciones por defecto del esquema (`TrackingFieldDefinition.Options`), y limpia `LastValue` si coincide; persiste solo lo que realmente cambio.
- Eliminar un proyecto (`DeleteProject`): borra su archivo (`TrackingProjectRepository.Delete`) y limpia la preferencia de "ultimo proyecto abierto" si apuntaba a el.
- Persistir cambios (`SaveProject`) y actualizar preferencias; la UI solo llama a esto al cambiar de proyecto o al cerrar la aplicacion (tras confirmar con el usuario), no en cada edicion de campo.
- Consultar y cambiar la carpeta donde se guardan los proyectos (`GetProjectsFolderPath`, `CompareProjectsFolder`, `ApplyProjectsFolderChange`, `LogFolderChangeDecision`): el selector de proyectos solo muestra los que existen fisicamente en la carpeta seleccionada (los que solo existian en la aplicacion no se copian a la carpeta nueva); los que existen en ambos lados con contenido distinto se reportan como conflicto para que la UI pida al usuario cual version conservar.
- Guardar una copia de seguridad con fecha/hora del proyecto activo al cerrar la aplicacion (`CreateExitBackup`).
- Filtrar la lista de proyectos visibles segun el usuario de Windows actual (`GetProjects`, `TryOpenLastProject`, `OpenProject`): un proyecto sin usuarios asignados es visible para todos; si tiene usuarios asignados (`TrackingProject.AllowedUserNames`), solo se muestra/abre si el usuario de Windows actual (`Environment.UserName`) coincide con uno de ellos, o con la parte del correo antes de la "@" cuando el usuario asignado es una direccion de correo.
- Gestionar los usuarios permitidos de un proyecto (`UpdateAllowedUsers`), normalizando (recortando espacios, quitando duplicados) y guardando el proyecto.

## Reglas
- Un servicio no debe contener logica de UI.
- Un servicio no debe contener detalles bajos de acceso a archivo.
- Dependencias inyectadas por constructor siempre que sea posible.

## Resultado De Operaciones
Se recomienda usar objetos de resultado (exito/error) para:
- Estandarizar mensajes de validacion.
- Evitar excepciones para flujo normal de negocio.
- Simplificar el manejo en formularios.
