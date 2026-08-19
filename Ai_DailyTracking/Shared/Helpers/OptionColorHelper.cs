using Ai_DailyTracking.Domain.Models;

namespace Ai_DailyTracking.Shared.Helpers;

// Resolves the configurable per-option color (TrackingFieldDefinition.OptionColors) shared by the ficha combos,
// the history list and the "Crear informe" grid/PDF, so the color coding stays consistent everywhere.
public static class OptionColorHelper
{
    public static Color GetColor(TrackingFieldDefinition field, string? optionValue)
    {
        if (field.OptionColors is not null &&
            !string.IsNullOrEmpty(optionValue) &&
            field.OptionColors.TryGetValue(optionValue, out var hexColor) &&
            TryParseHexColor(hexColor, out var parsedColor))
        {
            return parsedColor;
        }

        return Color.Gainsboro;
    }

    private static bool TryParseHexColor(string hexColor, out Color color)
    {
        try
        {
            color = ColorTranslator.FromHtml(hexColor);
            return true;
        }
        catch (Exception)
        {
            color = Color.Gainsboro;
            return false;
        }
    }
}
