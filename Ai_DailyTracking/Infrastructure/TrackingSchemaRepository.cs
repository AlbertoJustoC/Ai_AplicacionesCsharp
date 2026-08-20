using Ai_DailyTracking.Domain.Models;
using Ai_DailyTracking.Shared.Constants;

namespace Ai_DailyTracking.Infrastructure;

public sealed class TrackingSchemaRepository
{
    private static readonly string[] RequiredStatusOptions = ["Nuevo", "En curso", "En revisión", "Hecho", "Cerrado"];
    private static readonly Dictionary<string, string> RequiredStatusColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Nuevo"] = "#95A5A6",
        ["En curso"] = "#2E86C1",
        ["En revisión"] = "#8E44AD",
        ["Hecho"] = "#2E8B57",
        ["Cerrado"] = "#34495E"
    };

    private static readonly HashSet<string> LastValueDefaultKeys =
    [
        "discipline",
        "area",
        "package",
        "createdBy",
        "channel",
        "type",
        "assignedTo"
    ];

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

        var changed = false;

        if (SanitizeEditableOptionDefaults(schema))
        {
            changed = true;
        }

        if (ApplyRequestedFieldDefaults(schema))
        {
            changed = true;
        }

        if (changed)
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

    private static bool ApplyRequestedFieldDefaults(TrackingFormSchema schema)
    {
        var changed = false;

        foreach (var field in schema.Fields)
        {
            if (LastValueDefaultKeys.Contains(field.Key) && !field.DefaultToLastValue)
            {
                field.DefaultToLastValue = true;
                changed = true;
            }

            if (!string.Equals(field.Key, "status", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!field.Options.SequenceEqual(RequiredStatusOptions, StringComparer.OrdinalIgnoreCase))
            {
                field.Options = [.. RequiredStatusOptions];
                changed = true;
            }

            var currentColors = field.OptionColors ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (currentColors.Count != RequiredStatusColors.Count ||
                RequiredStatusColors.Any(required => !currentColors.TryGetValue(required.Key, out var value) || !string.Equals(value, required.Value, StringComparison.OrdinalIgnoreCase)))
            {
                field.OptionColors = new Dictionary<string, string>(RequiredStatusColors, StringComparer.OrdinalIgnoreCase);
                changed = true;
            }

            if (!string.IsNullOrWhiteSpace(field.LastValue) && !field.Options.Contains(field.LastValue, StringComparer.OrdinalIgnoreCase))
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
                new TrackingFieldDefinition { Key = "area", Label = "Area", Type = TrackingFieldType.EditableOption, Placeholder = "Ej. Planta Norte", DefaultToLastValue = true },
                new TrackingFieldDefinition { Key = "package", Label = "Paquete", Type = TrackingFieldType.EditableOption, Placeholder = "Ej. PKG-204", DefaultToLastValue = true },

                // Fila 2: Actividad (texto largo).
                new TrackingFieldDefinition { Key = "activity", Label = "Actividad", Type = TrackingFieldType.LongText, Required = true, Placeholder = "Describe el input o actividad recibida" },

                // Fila 3: Creado por, Canal, Tipo, Asignado a.
                new TrackingFieldDefinition { Key = "createdBy", Label = "Creado por", Type = TrackingFieldType.EditableOption, Placeholder = "Nombre de quien crea el registro", DefaultToLastValue = true },
                new TrackingFieldDefinition { Key = "channel", Label = "Canal", Type = TrackingFieldType.Option, Options = ["Email", "Teams", "ACC", "TrimbleConnect", "Papel", "Sin definir"], DefaultToLastValue = true },
                new TrackingFieldDefinition { Key = "type", Label = "Tipo", Type = TrackingFieldType.Option, Options = ["Cambio de Diseno", "Solicitud", "Comentario", "Incidencia", "Consulta", "Sin definir"], DefaultToLastValue = true },
                new TrackingFieldDefinition { Key = "assignedTo", Label = "Asignado a", Type = TrackingFieldType.EditableOption, Placeholder = "Nombre del ingeniero responsable", DefaultToLastValue = true },

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
                    Options = ["Nuevo", "En curso", "En revisión", "Hecho", "Cerrado"],
                    OptionColors = new Dictionary<string, string>
                    {
                        ["Nuevo"] = "#95A5A6",
                        ["En curso"] = "#2E86C1",
                        ["En revisión"] = "#8E44AD",
                        ["Hecho"] = "#2E8B57",
                        ["Cerrado"] = "#34495E"
                    }
                },
                new TrackingFieldDefinition { Key = "updatedDate", Label = "Ultimo cambio", Type = TrackingFieldType.Date, MinDateFieldKey = "recordDate" },

                // Fila 5: Notas (texto largo).
                new TrackingFieldDefinition { Key = "notes", Label = "Notas", Type = TrackingFieldType.LongText, Placeholder = "Comentarios, bloqueos o siguiente accion" }
            ]
        };
    }
}