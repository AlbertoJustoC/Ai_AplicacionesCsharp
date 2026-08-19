# ADR - Registro De Decisiones De Arquitectura

## Que Es Un ADR
Un ADR (Architecture Decision Record) documenta una decision tecnica relevante: el contexto, las opciones consideradas y por que se eligio una en concreto. Sirve para que futuras personas (o el propio equipo) entiendan el "por que" sin tener que reconstruir la conversacion original.

## Convencion De Nombres
`NNNN-titulo-corto-en-minusculas.md`, con `NNNN` como numero secuencial de 4 digitos.

## Indice De ADRs
- [0001-persistencia-json-por-proyecto.md](0001-persistencia-json-por-proyecto.md): por que cada proyecto se guarda como un archivo JSON independiente en lugar de XLSX o una base de datos.
- [0002-grafico-personalizado-con-gdi.md](0002-grafico-personalizado-con-gdi.md): por que el panel grafico se dibuja con GDI+ en vez de usar una libreria de charting externa.
- [0003-tablelayoutpanel-en-lugar-de-splitcontainer.md](0003-tablelayoutpanel-en-lugar-de-splitcontainer.md): por que el panel de contenido principal usa `TableLayoutPanel` en vez de `SplitContainer`.
- [0004-informe-pdf-con-printdocument.md](0004-informe-pdf-con-printdocument.md): por que el informe PDF se genera con `PrintDocument`/`PrintDialog` (impresora "Microsoft Print to PDF") en vez de sumar una libreria de PDF.

## Como Agregar Un Nuevo ADR
1. Copiar la estructura de un ADR existente (Contexto, Decision, Consecuencias).
2. Numerar de forma secuencial.
3. Enlazarlo desde este indice.
