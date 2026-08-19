namespace Ai_DailyTracking.Domain.Models;

public sealed class TrackingFieldDefinition
{
    public string Key { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public TrackingFieldType Type { get; set; } = TrackingFieldType.Text;

    public bool Required { get; set; }

    public string? Placeholder { get; set; }

    public List<string> Options { get; set; } = [];

    // When true, the editor starts a new row before this field instead of sharing a row with the previous compact field.
    public bool StartsNewRow { get; set; }

    // When true, new entries are pre-filled with LastValue instead of starting blank.
    public bool DefaultToLastValue { get; set; }

    public string? LastValue { get; set; }

    // Maps an option value to a hex color (e.g. "#C0392B") used to color-code the field editor.
    public Dictionary<string, string>? OptionColors { get; set; }

    // When set, this Date field's value cannot be earlier than the value of the referenced field.
    public string? MinDateFieldKey { get; set; }
}