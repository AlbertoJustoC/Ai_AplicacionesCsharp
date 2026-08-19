using Ai_preAgresso.Domain.Models;

namespace Ai_preAgresso.Application.Services;

public sealed class AutoCompleteProvider
{
    public AutoCompleteStringCollection Proyectos { get; } = new();
    public AutoCompleteStringCollection Actividades { get; } = new();
    public AutoCompleteStringCollection Descripciones { get; } = new();

    public void Refresh(IEnumerable<TimeEntry> entries)
    {
        var list = entries.ToList();
        ReplaceValues(Proyectos, list.Select(entry => entry.Proyecto));
        ReplaceValues(Actividades, list.Select(entry => entry.Actividad));
        ReplaceValues(Descripciones, list.Select(entry => entry.Descripcion));
    }

    private static void ReplaceValues(AutoCompleteStringCollection collection, IEnumerable<string> values)
    {
        collection.Clear();
        var distinct = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        collection.AddRange(distinct);
    }
}
