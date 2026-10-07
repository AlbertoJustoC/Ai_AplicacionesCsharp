using Ai_DailyTracking.Domain.Models;
using Ai_DailyTracking.Shared.Constants;
using Ai_DailyTracking.Shared.Helpers;

namespace Ai_DailyTracking.Infrastructure;

public sealed class TrackingProjectRepository
{
    private readonly JsonFileStore _jsonFileStore;
    private readonly AppPreferencesRepository _preferencesRepository;
    private readonly SharedPathService _sharedPathService;
    private readonly ProjectChangeStore _changeStore;
    private readonly ProjectPresenceStore _presenceStore;
    private Guid? _openProjectId;
    public ProjectMergeResult? LastMergeResult { get; private set; }
    public IReadOnlyList<ProjectPresence> OtherEditors { get; private set; } = [];

    public TrackingProjectRepository(JsonFileStore jsonFileStore, AppPreferencesRepository preferencesRepository, SharedPathService sharedPathService, ProjectChangeStore changeStore, ProjectPresenceStore presenceStore)
    {
        _jsonFileStore = jsonFileStore;
        _preferencesRepository = preferencesRepository;
        _sharedPathService = sharedPathService;
        _changeStore = changeStore;
        _presenceStore = presenceStore;
    }

    public TrackingProject Create(string projectName)
    {
        var project = new TrackingProject
        {
            ProjectId = Guid.NewGuid(),
            ProjectName = projectName.Trim(),
            CreatedAtLocal = DateTime.Now,
            UpdatedAtLocal = DateTime.Now
        };

        project.StorageFileName = ProjectFileNameHelper.BuildStorageFileName(project.ProjectName, project.ProjectId);
        var directory = GetEffectiveProjectsDirectory();
        if (!string.IsNullOrWhiteSpace(_preferencesRepository.Load().ProjectsFolderPath) && !Directory.Exists(directory))
            throw new DirectoryNotFoundException($"La carpeta de proyectos no está disponible en este equipo: {directory}");
        LastMergeResult = _changeStore.Create(project, directory);
        BeginPresence(project.ProjectId, directory);
        NormalizeSharedPaths(LastMergeResult.Project);
        _changeStore.UpdateEditingBasePaths(LastMergeResult.Project);
        return LastMergeResult.Project;
    }

    public IReadOnlyList<TrackingProject> GetAll()
    {
        var projectsDirectory = GetEffectiveProjectsDirectory();
        if (!string.IsNullOrWhiteSpace(_preferencesRepository.Load().ProjectsFolderPath) && !Directory.Exists(projectsDirectory))
        {
            throw new DirectoryNotFoundException($"La carpeta de proyectos no está disponible en este equipo: {projectsDirectory}");
        }
        Directory.CreateDirectory(projectsDirectory);

        var projects = _changeStore.GetAll(projectsDirectory);
        foreach (var project in projects) NormalizeSharedPaths(project);
        return projects;
    }

    public TrackingProject? GetById(Guid projectId)
    {
        var folder = GetEffectiveProjectsDirectory();
        LastMergeResult = _changeStore.Open(projectId, folder);
        var project = LastMergeResult?.Project;
        if (project is not null)
        {
            BeginPresence(projectId, folder);
            NormalizeSharedPaths(project);
            _changeStore.UpdateEditingBasePaths(project);
        }
        return project;
    }

    public ProjectMergeResult Save(TrackingProject project)
    {
        if (string.IsNullOrWhiteSpace(project.StorageFileName))
        {
            project.StorageFileName = ProjectFileNameHelper.BuildStorageFileName(project.ProjectName, project.ProjectId);
        }

        project.UpdatedAtLocal = DateTime.Now;

        NormalizeSharedPaths(project);
        LastMergeResult = _changeStore.Save(project, GetEffectiveProjectsDirectory());
        RefreshPresence();
        NormalizeSharedPaths(LastMergeResult.Project);
        _changeStore.UpdateEditingBasePaths(LastMergeResult.Project);
        return LastMergeResult;
    }

    public void Delete(TrackingProject project)
    {
        _changeStore.DeleteProject(project, GetEffectiveProjectsDirectory());
        if (_openProjectId == project.ProjectId) ReleaseCurrentProject();
    }

    public string GetEffectiveProjectsDirectory()
    {
        var preferences = _preferencesRepository.Load();
        return string.IsNullOrWhiteSpace(preferences.ProjectsFolderPath)
            ? AppStoragePaths.ProjectsDirectory
            : _sharedPathService.Resolve(preferences.ProjectsFolderPath, directory: true)
              ?? throw new DirectoryNotFoundException("Configura la raíz local de SharePoint antes de abrir los proyectos.");
    }

    // Only switches which folder is read from/written to; does not copy or move any project files.
    public void SetProjectsFolderPath(string newFolderPath)
    {
        var preferences = _preferencesRepository.Load();
        var storedPath = _sharedPathService.ToStoredPath(newFolderPath, directory: true);
        preferences.ProjectsFolderPath = storedPath;
        var recentPaths = preferences.RecentProjectsFolderPaths ?? [];
        preferences.RecentProjectsFolderPaths = [storedPath, .. recentPaths
            .Where(path => !string.Equals(path, storedPath, StringComparison.OrdinalIgnoreCase))
            .Take(9)];
        _preferencesRepository.Save(preferences);
    }

    public IReadOnlyList<string> GetRecentProjectsFolderPaths()
    {
        var preferences = _preferencesRepository.Load();
        if (preferences.RecentProjectsFolderPaths is null)
        {
            preferences.RecentProjectsFolderPaths = [preferences.ProjectsFolderPath ?? AppStoragePaths.ProjectsDirectory];
            _preferencesRepository.Save(preferences);
        }

        return preferences.RecentProjectsFolderPaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => _sharedPathService.Resolve(path, directory: true))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public void RemoveRecentProjectsFolderPath(string path)
    {
        var preferences = _preferencesRepository.Load();
        preferences.RecentProjectsFolderPaths = (preferences.RecentProjectsFolderPaths ?? [])
            .Where(item => !string.Equals(_sharedPathService.Resolve(item, directory: true), path, StringComparison.OrdinalIgnoreCase))
            .ToList();
        _preferencesRepository.Save(preferences);
    }

    public void SaveBackupCopy(TrackingProject project)
    {
        Directory.CreateDirectory(AppStoragePaths.BackupDirectory);
        var fileName = ProjectFileNameHelper.BuildBackupFileName(project.ProjectName, DateTime.Now);
        var filePath = Path.Combine(AppStoragePaths.BackupDirectory, fileName);
        _jsonFileStore.Write(filePath, project);
    }

    public void ReleaseCurrentProject()
    {
        _changeStore.Close(_openProjectId);
        TryClosePresence();
        _openProjectId = null;
        OtherEditors = [];
    }

    public IReadOnlyList<ProjectPresence> RefreshPresence()
    {
        if (_openProjectId is null) return [];
        try { OtherEditors = _presenceStore.Heartbeat(_openProjectId.Value, GetEffectiveProjectsDirectory()); }
        catch (IOException) { OtherEditors = []; }
        catch (UnauthorizedAccessException) { OtherEditors = []; }
        return OtherEditors;
    }

    private void BeginPresence(Guid projectId, string folder)
    {
        if (_openProjectId != projectId) TryClosePresence();
        _openProjectId = projectId;
        try { OtherEditors = _presenceStore.Open(projectId, folder); }
        catch (IOException) { OtherEditors = []; }
        catch (UnauthorizedAccessException) { OtherEditors = []; }
    }

    private void TryClosePresence()
    {
        try { _presenceStore.Close(); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    public ProjectMergeResult ResolveConflict(ProjectFieldConflict conflict, Guid selectedChangeId)
    {
        if (_openProjectId is null) throw new InvalidOperationException("No hay ningún proyecto abierto.");
        LastMergeResult = _changeStore.ResolveConflict(_openProjectId.Value, GetEffectiveProjectsDirectory(), conflict, selectedChangeId);
        NormalizeSharedPaths(LastMergeResult.Project);
        _changeStore.UpdateEditingBasePaths(LastMergeResult.Project);
        return LastMergeResult;
    }

    private void NormalizeSharedPaths(TrackingProject project)
    {
        if (!string.IsNullOrWhiteSpace(project.LastEntryFolderPath))
        {
            project.LastEntryFolderPath = _sharedPathService.ToStoredPath(project.LastEntryFolderPath, directory: true);
        }

        if (!string.IsNullOrWhiteSpace(project.LastMarkdownParentPath))
        {
            project.LastMarkdownParentPath = _sharedPathService.ToStoredPath(project.LastMarkdownParentPath, directory: true);
        }

        foreach (var entry in project.Entries)
        {
            foreach (var key in new[] { "inputReceivedPath", "markdownFilePath" })
            {
                if (entry.Values.TryGetValue(key, out var path) && !string.IsNullOrWhiteSpace(path))
                {
                    entry.Values[key] = _sharedPathService.ToStoredPath(path, directory: key == "inputReceivedPath");
                }
            }
        }
    }
}
