using Ai_preAgresso.Domain.Models;
using Ai_preAgresso.Infrastructure;

namespace Ai_preAgresso.Application.Services;

public sealed class AgressoWorkspaceService
{
    private readonly TimeEntryRepository _repository;

    public AutoCompleteProvider AutoComplete { get; } = new();

    public AgressoWorkspaceService(TimeEntryRepository repository)
    {
        _repository = repository;
        AutoComplete.Refresh(_repository.LoadAll());
    }

    public List<TimeEntry> GetAllEntries() => _repository.LoadAll();

    public List<TimeEntry> GetEntriesForWeek(int isoYear, int isoWeek)
    {
        var weekDates = new HashSet<DateOnly>(WeekPeriodCalculator.GetWeekdays(isoYear, isoWeek));
        return GetAllEntries().Where(entry => weekDates.Contains(entry.Fecha)).ToList();
    }

    // Replaces every entry that falls within the given ISO week with the edited set; entries in other weeks are untouched.
    public void SaveWeek(int isoYear, int isoWeek, List<TimeEntry> weekEntries)
    {
        var weekDates = new HashSet<DateOnly>(WeekPeriodCalculator.GetWeekdays(isoYear, isoWeek));
        var all = GetAllEntries();
        all.RemoveAll(entry => weekDates.Contains(entry.Fecha));
        all.AddRange(weekEntries);
        _repository.SaveAll(all);
        AutoComplete.Refresh(all);
    }

    public void ImportEntries(IEnumerable<TimeEntry> imported)
    {
        var all = GetAllEntries();
        foreach (var entry in imported)
        {
            entry.Id = Guid.NewGuid();
            all.Add(entry);
        }
        _repository.SaveAll(all);
        AutoComplete.Refresh(all);
    }
}
