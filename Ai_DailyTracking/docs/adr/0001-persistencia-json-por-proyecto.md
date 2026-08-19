# 0001 - Persistencia JSON Por Proyecto

## Estado
Aceptado.

## Contexto
La aplicacion necesita guardar los registros de seguimiento diario sin que el usuario tenga que pulsar "Guardar" nunca: cada cambio debe reflejarse de inmediato en un archivo independiente por proyecto. Las opciones consideradas fueron: XLSX (via ClosedXML/EPPlus/NPOI), una base de datos embebida (SQLite) y JSON plano por proyecto.

## Decision
Cada proyecto se persiste como un archivo `.json` independiente en `%AppData%\AiDailyTracking\Projects\`, nombrado como `<nombre-proyecto>-<id>.json`. La preferencia del ultimo proyecto abierto y el esquema de campos tambien se guardan como JSON separados.

## Motivos
- Autoguardado inmediato: escribir un JSON completo tras cada cambio es simple y rapido, sin necesidad de gestionar transacciones o bloqueos de archivo como ocurriria con un `.xlsx` abierto.
- Inspeccion manual sencilla: cualquier ingeniero puede abrir el archivo con un editor de texto para verificar datos sin depender de Excel instalado.
- Evita bloqueos de archivo: un `.xlsx` puede quedar bloqueado si el usuario lo tiene abierto en Excel; un `.json` propio de la app no tiene ese problema.
- Esquema flexible: los valores de cada registro se guardan como diccionario `clave -> valor`, lo que permite ajustar los campos del formulario (`tracking-schema.json`) sin migrar datos ni recompilar.

## Consecuencias
- Queda pendiente una futura exportacion/importacion a XLSX si el usuario necesita compartir los datos en ese formato (no bloqueado por esta decision, solo pospuesto).
- Al no haber un motor de consultas, los filtros y agrupaciones (por ejemplo en el panel grafico) se calculan en memoria sobre la lista de registros del proyecto activo.
