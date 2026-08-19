using Ai_DailyTracking.Domain.Models;
using Ai_DailyTracking.Shared.Constants;

namespace Ai_DailyTracking.Infrastructure;

public sealed class TrackingSchemaRepository
{
    private readonly JsonFileStore _jsonFileStore;

    public TrackingSchemaRepository(JsonFileStore jsonFileStore)
    {
        _jsonFileStore = jsonFileStore;
    }

    public TrackingFormSchema Load()
    {
        if (!File.Exists(AppStoragePaths.SchemaFile))
        {
            var defaultSchema = CreateDefaultSchema();
            _jsonFileStore.Write(AppStoragePaths.SchemaFile, defaultSchema);
            return defaultSchema;
        }

        var schema = _jsonFileStore.ReadOrDefault(AppStoragePaths.SchemaFile, CreateDefaultSchema());

        if (schema.Fields.Count == 0)
        {
            return CreateDefaultSchema();
        }

        if (SanitizeEditableOptionDefaults(schema))
        {
            Save(schema);
        }

        return schema;
    }

    // One-time cleanup for schemas saved before EditableOption values became project-scoped: strips values
    // that had leaked into the shared default list, restoring each field to its true hardcoded defaults.
    private static bool SanitizeEditableOptionDefaults(TrackingFormSchema schema)
    {
        var defaultsByKey = CreateDefaultSchema().Fields.ToDictionary(field => field.Key, StringComparer.OrdinalIgnoreCase);
        var changed = false;

        foreach (var field in schema.Fields)
        {
            if (field.Type != TrackingFieldType.EditableOption || !defaultsByKey.TryGetValue(field.Key, out var defaultField))
            {
                continue;
            }

            if (!field.Options.SequenceEqual(defaultField.Options, StringComparer.OrdinalIgnoreCase))
            {
                field.Options = [.. defaultField.Options];
                changed = true;
            }

            if (field.LastValue is not null && !field.Options.Contains(field.LastValue, StringComparer.OrdinalIgnoreCase))
            {
                field.LastValue = null;
                changed = true;
            }
        }

        return changed;
    }

    public void Save(TrackingFormSchema schema)
    {
        _jsonFileStore.Write(AppStoragePaths.SchemaFile, schema);
    }

    private static TrackingFormSchema CreateDefaultSchema()
    {
        return new TrackingFormSchema
        {
            Name = "Tracking",
            Fields =
            [
                // Fila 1: Fecha, Disciplina, Area, Paquete.
                new TrackingFieldDefinition { Key = "recordDate", Label = "Fecha", Type = TrackingFieldType.Date, Required = true },
                new TrackingFieldDefinition { Key = "discipline", Label = "Disciplina", Type = TrackingFieldType.EditableOption, Required = true, Options = ["Civil", "Estructura", "Arquitectura", "Instalaciones"], DefaultToLastValue = true },
                new TrackingFieldDefinition { Key = "area", Label = "Area", Type = TrackingFieldType.EditableOption, Placeholder = "Ej. Planta Norte" },
                new TrackingFieldDefinition { Key = "package", Label = "Paquete", Type = TrackingFieldType.EditableOption, Placeholder = "Ej. PKG-204" },

                // Fila 2: Actividad (texto largo).
                new TrackingFieldDefinition { Key = "activity", Label = "Actividad", Type = TrackingFieldType.LongText, Required = true, Placeholder = "Describe el input o actividad recibida" },

                // Fila 3: Creado por, Canal, Tipo, Asignado a.
                new TrackingFieldDefinition { Key = "createdBy", Label = "Creado por", Type = TrackingFieldType.EditableOption, Placeholder = "Nombre de quien crea el registro" },
                new TrackingFieldDefinition { Key = "channel", Label = "Canal", Type = TrackingFieldType.Option, Options = ["Email", "Teams", "ACC", "TrimbleConnect", "Papel", "Sin definir"] },
                new TrackingFieldDefinition { Key = "type", Label = "Tipo", Type = TrackingFieldType.Option, Options = ["Cambio de Diseno", "Solicitud", "Comentario", "Incidencia", "Consulta", "Sin definir"] },
                new TrackingFieldDefinition { Key = "assignedTo", Label = "Asignado a", Type = TrackingFieldType.EditableOption, Placeholder = "Nombre del ingeniero responsable" },

                // Fila 4: Prioridad, Complejidad, Estado, Ultimo cambio.
                new TrackingFieldDefinition
                {
                    Key = "priority", Label = "Prioridad", Type = TrackingFieldType.Option, StartsNewRow = true,
                    Options = ["Baja", "Media", "Alta", "Critica"],
                    OptionColors = new Dictionary<string, string>
                    {
                        ["Baja"] = "#2E8B57",
                        ["Media"] = "#F1C40F",
                        ["Alta"] = "#E67E22",
                        ["Critica"] = "#C0392B"
                    }
                },
                new TrackingFieldDefinition
                {
                    Key = "complexity", Label = "Complejidad", Type = TrackingFieldType.Option,
                    Options = ["Baja", "Media", "Alta"],
                    OptionColors = new Dictionary<string, string>
                    {
                        ["Baja"] = "#2E8B57",
                        ["Media"] = "#F1C40F",
                        ["Alta"] = "#C0392B"
                    }
                },
                new TrackingFieldDefinition
                {
                    Key = "status", Label = "Estado", Type = TrackingFieldType.Option, Required = true,
                    Options = ["Pendiente", "En curso", "En revision", "Bloqueado", "Cerrado"],
                    OptionColors = new Dictionary<string, string>
                    {
                        ["Pendiente"] = "#95A5A6",
                        ["En curso"] = "#2E86C1",
                        ["En revision"] = "#8E44AD",
                        ["Bloqueado"] = "#C0392B",
                        ["Cerrado"] = "#2E8B57"
                    }
                },
                new TrackingFieldDefinition { Key = "updatedDate", Label = "Ultimo cambio", Type = TrackingFieldType.Date, MinDateFieldKey = "recordDate" },

                // Fila 5: Notas (texto largo).
                new TrackingFieldDefinition { Key = "notes", Label = "Notas", Type = TrackingFieldType.LongText, Placeholder = "Comentarios, bloqueos o siguiente accion" }
            ]
        };
    }
}