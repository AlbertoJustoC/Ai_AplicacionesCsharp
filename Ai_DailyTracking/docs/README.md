# Documentacion de Ai_DailyTracking

## Vision General
Ai_DailyTracking es una aplicacion de escritorio (WinForms, C#) orientada al registro y seguimiento diario de datos con soporte de exportacion/lectura en Excel (XLSX).

El proyecto esta organizado para crecer de forma mantenible: responsabilidades separadas, clases pequenas por archivo y carpetas por dominio.

## Estado Actual
El repositorio se encuentra en una fase inicial de construccion. Esta documentacion establece la base de arquitectura, convenciones y guias de desarrollo para acompanar la evolucion de la app.

## Indice De Documentos
- [01-overview.md](01-overview.md): objetivo funcional, alcance, flujo general y casos de uso.
- [02-ui.md](02-ui.md): capa de interfaz (formularios/controles), eventos y responsabilidades de UI.
- [03-application-services.md](03-application-services.md): servicios de aplicacion y reglas de negocio.
- [04-domain-models.md](04-domain-models.md): entidades, DTOs y validaciones de dominio.
- [05-infrastructure.md](05-infrastructure.md): acceso a archivos, Excel e integraciones tecnicas.
- [06-conventions.md](06-conventions.md): convenciones de codigo, estructura y evolucion.
- [07-testing.md](07-testing.md): estrategia de pruebas unitarias e integracion.
- [08-troubleshooting.md](08-troubleshooting.md): errores comunes de build, arranque y rutas de archivos.
- [adr/README.md](adr/README.md): registro de decisiones de arquitectura (ADR).
- [CHANGELOG.md](CHANGELOG.md): historial resumido de cambios tecnicos importantes.

## Estructura De Carpetas Recomendada
Esta estructura refleja como debe distribuirse la aplicacion mientras crece:

```text
Ai_DailyTracking/
|-- UI/
|   |-- Forms/
|   `-- Controls/
|-- Application/
|   `-- Services/
|-- Domain/
|   `-- Models/
|-- Infrastructure/
|-- Shared/
|   |-- Helpers/
|   `-- Constants/
|-- docs/
|   |-- README.md
|   |-- 01-overview.md
|   |-- 02-ui.md
|   |-- 03-application-services.md
|   |-- 04-domain-models.md
|   |-- 05-infrastructure.md
|   |-- 06-conventions.md
|   |-- 07-testing.md
|   |-- 08-troubleshooting.md
|   |-- CHANGELOG.md
|   `-- adr/
|       |-- README.md
|       |-- 0001-persistencia-json-por-proyecto.md
|       |-- 0002-grafico-personalizado-con-gdi.md
|       |-- 0003-tablelayoutpanel-en-lugar-de-splitcontainer.md
|       `-- 0004-informe-pdf-con-printdocument.md
`-- .github/
    `-- copilot-instructions.md
```

## Notas De Compilacion
1. Requisito recomendado: .NET SDK 8.0 o superior.
2. Abrir la solucion/proyecto en Visual Studio o ejecutar desde terminal en la raiz del proyecto.
3. Comandos comunes (cuando exista el .csproj/solucion):

```powershell
dotnet restore
dotnet build -c Release
dotnet run
```

4. Si hay dependencias de Excel (por ejemplo, ClosedXML, EPPlus, NPOI), agregarlas explicitamente en el proyecto y documentar version/licencia.

## Convenciones Importantes
- Una clase publica por archivo siempre que sea practico.
- Nombres de clases orientados a responsabilidad (ejemplo: DailyTrackingExportService).
- Evitar logica de negocio en el code-behind del formulario.
- Extraer reglas a Application/Services y helpers reutilizables.
- Mantener metodos cortos y composables.
- Favorecer cambios incrementales y legibles.

## Documentacion Complementaria
Ademas de la base anterior, el proyecto incluye:

1. Registro de decisiones de arquitectura (ADR): [adr/README.md](adr/README.md).
2. Guia de pruebas: [07-testing.md](07-testing.md).
3. Guia de errores y troubleshooting: [08-troubleshooting.md](08-troubleshooting.md).
4. Changelog interno: [CHANGELOG.md](CHANGELOG.md).
