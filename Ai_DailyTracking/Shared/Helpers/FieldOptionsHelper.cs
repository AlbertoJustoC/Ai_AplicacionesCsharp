using Ai_DailyTracking.Domain.Models;

namespace Ai_DailyTracking.Shared.Helpers;

// Combines a field's schema-level default options with the extra values learned for one specific project,
// keeping EditableOption values scoped per project instead of shared globally across all projects.
public static class FieldOptionsHelper
{
    public static IReadOnlyList<string> GetOptions(TrackingProject project, TrackingFieldDefinition field)
    {
        var options = new List<string>(field.Options);

        if (project.CustomFieldOptions.TryGetValue(field.Key, out var customOptions))
        {
            foreach (var customOption in customOptions)
            {
                if (!options.Any(option => string.Equals(option, customOption, StringComparison.OrdinalIgnoreCase)))
                {
                    options.Add(customOption);
                }
            }
        }

        return options;
    }
}
