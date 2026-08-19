using Ai_DailyTracking.Domain.Models;
using Ai_DailyTracking.Infrastructure;
using Ai_DailyTracking.Shared.Helpers;

namespace Ai_DailyTracking.Application.Services;

public sealed class ProjectWorkspaceService
{
    private readonly TrackingProjectRepository _projectRepository;
    private readonly AppPreferencesRepository _preferencesRepository;
    private readonly TrackingSchemaRepository _schemaRepository;

    public ProjectWorkspaceService(
        TrackingProjectRepository projectRepository,
        AppPreferencesRepository preferencesRepository,
        TrackingSchemaRepository schemaRepository)
    {
        _projectRepository = projectRepository;
        _preferencesRepository = preferencesRepository;
        _schemaRepository = schemaRepository;
    }

    public TrackingEntry CreateEntry(TrackingProject project, TrackingFormSchema schema)
    {
        var entry = new TrackingEntry
        {
            EntryNumber = project.Entries.Count == 0 ? 1 : project.Entries.Max(item => item.EntryNumber) + 1
        };

        foreach (var field in schema.Fields.Where(field => field.DefaultToLastValue && !string.IsNullOrWhiteSpace(field.LastValue)))
        {
            entry.Values[field.Key] = field.LastValue;
        }

        project.Entries.Insert(0, entry);
        return entry;
    }

    public TrackingProject CreateProject(string projectName)
    {
        var project = _projectRepository.Create(projectName);
        SetLastProject(project);
        return project;
    }

    public void DeleteEntry(TrackingProject project, TrackingEntry entry)
    {
        project.Entries.RemoveAll(item => item.EntryId == entry.EntryId);
        RenumberEntries(project);
    }

    // Permanently deletes a project's file; clears the "last opened project" preference if it pointed to it.
    public void DeleteProject(TrackingProject project)
    {
        _projectRepository.Delete(project);

        var preferences = _preferencesRepository.Load();

        if (preferences.LastProjectId == project.ProjectId)
        {
            preferences.LastProjectId = null;
            _preferencesRepository.Save(preferences);
        }
    }

    public string GetProjectsFolderPath()
    {
        return _projectRepository.GetEffectiveProjectsDirectory();
    }

    // Compares the projects currently loaded in the app against the ones found in the candidate folder.
    public ProjectFolderComparisonResult CompareProjectsFolder(string newFolderPath)
    {
        return _projectRepository.CompareWithFolder(newFolderPath);
    }

    // Merges (keeping the most recent version of each project) and switches to the new folder.
    public void ApplyProjectsFolderChange(ProjectFolderComparisonResult comparison)
    {
        _projectRepository.ApplyFolderChange(comparison.NewFolderPath, comparison.ProjectsToKeep);
    }

    public void LogFolderChangeDecision(ProjectFolderComparisonResult comparison, bool accepted)
    {
        _projectRepository.WriteFolderChangeLog(comparison, accepted);
    }

    // Writes a timestamped copy of the given project to the backup folder; called when the app is closing.
    public void CreateExitBackup(TrackingProject? project)
    {
        if (project is null)
        {
            return;
        }

        _projectRepository.SaveBackupCopy(project);
    }

    public IReadOnlyList<TrackingProject> GetProjects()
    {
        return _projectRepository.GetAll().Where(IsVisibleToCurrentUser).ToList();
    }

    // Replaces the list of users allowed to see the project (empty list means visible to everyone) and saves it.
    public void UpdateAllowedUsers(TrackingProject project, IEnumerable<string> userNames)
    {
        project.AllowedUserNames = userNames
            .Select(userName => userName.Trim())
            .Where(userName => !string.IsNullOrWhiteSpace(userName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        SaveProject(project);
    }

    // A project is visible when no users are assigned (public) or the current Windows user is one of the assigned ones.
    // Assigned entries may be a plain Windows username or an email; the email's part before "@" is also compared,
    // since the Windows logon name (Environment.UserName) rarely matches a full email address.
    private static bool IsVisibleToCurrentUser(TrackingProject project)
    {
        return project.AllowedUserNames.Count == 0 ||
            project.AllowedUserNames.Any(userName => MatchesCurrentUser(userName, Environment.UserName));
    }

    private static bool MatchesCurrentUser(string allowedUserName, string currentUserName)
    {
        if (string.Equals(allowedUserName, currentUserName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var emailLocalPart = allowedUserName.Split('@')[0];
        return string.Equals(emailLocalPart, currentUserName, StringComparison.OrdinalIgnoreCase);
    }

    public TrackingFormSchema LoadSchema()
    {
        return _schemaRepository.Load();
    }

    public TrackingProject? OpenProject(Guid projectId)
    {
        var project = _projectRepository.GetById(projectId);

        if (project is not null && !IsVisibleToCurrentUser(project))
        {
            return null;
        }

        if (project is not null)
        {
            if (EnsureEntryNumbers(project))
            {
                _projectRepository.Save(project);
            }

            SetLastProject(project);
        }

        return project;
    }

    // Remembers a value entered in an editable-option field (scoped to this project, so other projects keep only
    // the schema's default options) and/or the last value picked in a default-to-last-value field (schema-wide).
    public bool RememberFieldValue(TrackingFormSchema schema, TrackingProject project, TrackingFieldDefinition field, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmedValue = value.Trim();
        var changed = false;

        if (field.Type == TrackingFieldType.EditableOption &&
            !FieldOptionsHelper.GetOptions(project, field).Any(option => string.Equals(option, trimmedValue, StringComparison.OrdinalIgnoreCase)))
        {
            if (!project.CustomFieldOptions.TryGetValue(field.Key, out var customOptions))
            {
                customOptions = [];
                project.CustomFieldOptions[field.Key] = customOptions;
            }

            customOptions.Add(trimmedValue);
            _projectRepository.Save(project);
            changed = true;
        }

        if (field.DefaultToLastValue && !string.Equals(field.LastValue, trimmedValue, StringComparison.Ordinal))
        {
            field.LastValue = trimmedValue;
            _schemaRepository.Save(schema);
            changed = true;
        }

        return changed;
    }

    // Removes a value from an editable-option field's dropdown: from this project's own learned values and/or
    // from the schema's shared defaults, persisting whichever storage actually changed.
    public void ForgetFieldValue(TrackingFormSchema schema, TrackingProject project, TrackingFieldDefinition field, string value)
    {
        if (field.Type != TrackingFieldType.EditableOption || string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var changedProject = false;

        if (project.CustomFieldOptions.TryGetValue(field.Key, out var customOptions) &&
            customOptions.RemoveAll(option => string.Equals(option, value, StringComparison.OrdinalIgnoreCase)) > 0)
        {
            changedProject = true;
        }

        var changedSchema = field.Options.RemoveAll(option => string.Equals(option, value, StringComparison.OrdinalIgnoreCase)) > 0;

        if (field.LastValue is not null && string.Equals(field.LastValue, value, StringComparison.OrdinalIgnoreCase))
        {
            field.LastValue = null;
            changedSchema = true;
        }

        if (changedProject)
        {
            _projectRepository.Save(project);
        }

        if (changedSchema)
        {
            _schemaRepository.Save(schema);
        }
    }

    public void SaveProject(TrackingProject project)
    {
        _projectRepository.Save(project);
        SetLastProject(project);
    }

    public TrackingProject? TryOpenLastProject()
    {
        var preferences = _preferencesRepository.Load();

        if (preferences.LastProjectId is null)
        {
            return null;
        }

        var project = _projectRepository.GetById(preferences.LastProjectId.Value);

        if (project is not null && !IsVisibleToCurrentUser(project))
        {
            return null;
        }

        if (project is not null && EnsureEntryNumbers(project))
        {
            _projectRepository.Save(project);
        }

        return project;
    }

    private static bool EnsureEntryNumbers(TrackingProject project)
    {
        if (project.Entries.All(entry => entry.EntryNumber > 0))
        {
            return false;
        }

        var nextNumber = 1;

        foreach (var entry in project.Entries.OrderBy(entry => entry.CreatedAtLocal))
        {
            if (entry.EntryNumber <= 0)
            {
                entry.EntryNumber = nextNumber;
            }

            nextNumber = Math.Max(nextNumber, entry.EntryNumber) + 1;
        }

        return true;
    }

    private static void RenumberEntries(TrackingProject project)
    {
        var nextNumber = 1;

        foreach (var entry in project.Entries.OrderBy(entry => entry.EntryNumber))
        {
            entry.EntryNumber = nextNumber;
            nextNumber++;
        }
    }

    private void SetLastProject(TrackingProject project)
    {
        var preferences = _preferencesRepository.Load();
        preferences.LastProjectId = project.ProjectId;
        _preferencesRepository.Save(preferences);
    }
}