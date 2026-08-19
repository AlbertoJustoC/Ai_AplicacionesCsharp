using Ai_DailyTracking.Domain.Models;
using Ai_DailyTracking.Shared.Constants;
using Ai_DailyTracking.Shared.Helpers;

namespace Ai_DailyTracking.Infrastructure;

public sealed class TrackingProjectRepository
{
    private readonly JsonFileStore _jsonFileStore;
    private readonly AppPreferencesRepository _preferencesRepository;

    public TrackingProjectRepository(JsonFileStore jsonFileStore, AppPreferencesRepository preferencesRepository)
    {
        _jsonFileStore = jsonFileStore;
        _preferencesRepository = preferencesRepository;
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
        Save(project);
        return project;
    }

    public IReadOnlyList<TrackingProject> GetAll()
    {
        var projectsDirectory = GetEffectiveProjectsDirectory();
        Directory.CreateDirectory(projectsDirectory);

        var projects = new List<TrackingProject>();

        foreach (var filePath in Directory.GetFiles(projectsDirectory, "*.json"))
        {
            var project = _jsonFileStore.ReadOrDefault<TrackingProject?>(filePath, null);

            if (project is null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(project.StorageFileName))
            {
                project.StorageFileName = Path.GetFileName(filePath);
            }

            projects.Add(project);
        }

        return projects
            .OrderByDescending(project => project.UpdatedAtLocal)
            .ToList();
    }

    public TrackingProject? GetById(Guid projectId)
    {
        return GetAll().FirstOrDefault(project => project.ProjectId == projectId);
    }

    public void Save(TrackingProject project)
    {
        if (string.IsNullOrWhiteSpace(project.StorageFileName))
        {
            project.StorageFileName = ProjectFileNameHelper.BuildStorageFileName(project.ProjectName, project.ProjectId);
        }

        project.UpdatedAtLocal = DateTime.Now;

        var filePath = Path.Combine(GetEffectiveProjectsDirectory(), project.StorageFileName);
        _jsonFileStore.Write(filePath, project);
    }

    public void Delete(TrackingProject project)
    {
        var filePath = Path.Combine(GetEffectiveProjectsDirectory(), project.StorageFileName);

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }

    public string GetEffectiveProjectsDirectory()
    {
        var preferences = _preferencesRepository.Load();
        return string.IsNullOrWhiteSpace(preferences.ProjectsFolderPath)
            ? AppStoragePaths.ProjectsDirectory
            : preferences.ProjectsFolderPath;
    }

    public IReadOnlyList<TrackingProject> LoadProjectsFrom(string folderPath)
    {
        if (!Directory.Exists(folderPath))
        {
            return Array.Empty<TrackingProject>();
        }

        var projects = new List<TrackingProject>();

        foreach (var filePath in Directory.GetFiles(folderPath, "*.json"))
        {
            var project = _jsonFileStore.ReadOrDefault<TrackingProject?>(filePath, null);

            if (project is null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(project.StorageFileName))
            {
                project.StorageFileName = Path.GetFileName(filePath);
            }

            projects.Add(project);
        }

        return projects;
    }

    // Compares projects currently loaded in the app against the ones found in a candidate folder. Only projects that
    // physically exist in the candidate folder are kept (so the selector only shows what's in the selected folder);
    // projects that exist in both with different content are reported as conflicts for the caller to resolve.
    public ProjectFolderComparisonResult CompareWithFolder(string newFolderPath)
    {
        var currentFolderPath = GetEffectiveProjectsDirectory();
        var appProjects = GetAll();
        var folderProjects = LoadProjectsFrom(newFolderPath);
        var appProjectsById = appProjects.ToDictionary(project => project.ProjectId);

        var result = new ProjectFolderComparisonResult
        {
            CurrentFolderPath = currentFolderPath,
            NewFolderPath = newFolderPath
        };

        foreach (var folderProject in folderProjects)
        {
            if (!appProjectsById.TryGetValue(folderProject.ProjectId, out var appProject))
            {
                result.Differences.Add($"\"{folderProject.ProjectName}\": solo existe en la carpeta nueva; se mantendra.");
                result.ProjectsToKeep.Add(folderProject);
                continue;
            }

            if (appProject.UpdatedAtLocal == folderProject.UpdatedAtLocal)
            {
                result.ProjectsToKeep.Add(folderProject);
                continue;
            }

            result.Differences.Add(
                $"\"{appProject.ProjectName}\": version distinta entre la aplicacion ({appProject.UpdatedAtLocal:g}) y la carpeta ({folderProject.UpdatedAtLocal:g}); pendiente de elegir.");
            result.Conflicts.Add(new ProjectFolderConflict { AppVersion = appProject, FolderVersion = folderProject });
        }

        var folderProjectIds = new HashSet<Guid>(folderProjects.Select(project => project.ProjectId));

        foreach (var appProject in appProjects)
        {
            if (!folderProjectIds.Contains(appProject.ProjectId))
            {
                result.Differences.Add($"\"{appProject.ProjectName}\": solo existe en la aplicacion; no se copiara a la carpeta nueva.");
            }
        }

        return result;
    }


    // Writes the merged (most recent) set of projects into the new folder and switches future reads/writes to it.
    public void ApplyFolderChange(string newFolderPath, IReadOnlyList<TrackingProject> projectsToKeep)
    {
        Directory.CreateDirectory(newFolderPath);

        foreach (var project in projectsToKeep)
        {
            if (string.IsNullOrWhiteSpace(project.StorageFileName))
            {
                project.StorageFileName = ProjectFileNameHelper.BuildStorageFileName(project.ProjectName, project.ProjectId);
            }

            var filePath = Path.Combine(newFolderPath, project.StorageFileName);
            _jsonFileStore.Write(filePath, project);
        }

        var preferences = _preferencesRepository.Load();
        preferences.ProjectsFolderPath = newFolderPath;
        _preferencesRepository.Save(preferences);
    }

    // Appends the comparison result and the user's accept/cancel decision to a single history .log file kept in the project folder.
    public void WriteFolderChangeLog(ProjectFolderComparisonResult comparison, bool accepted)
    {
        var logFolderPath = comparison.NewFolderPath;
        Directory.CreateDirectory(logFolderPath);

        var filePath = Path.Combine(logFolderPath, "cambio-carpeta-historial.log");

        var lines = new List<string>
        {
            $"===== {DateTime.Now:yyyy-MM-dd HH:mm:ss} =====",
            $"Carpeta actual: {comparison.CurrentFolderPath}",
            $"Carpeta nueva: {comparison.NewFolderPath}",
            $"Decision: {(accepted ? "Aceptado" : "Cancelado")}",
            "Diferencias encontradas:"
        };

        lines.AddRange(comparison.Differences.Count == 0 ? new[] { "(ninguna)" } : comparison.Differences);
        lines.Add(string.Empty);

        File.AppendAllLines(filePath, lines);
    }

    public void SaveBackupCopy(TrackingProject project)
    {
        Directory.CreateDirectory(AppStoragePaths.BackupDirectory);
        var fileName = ProjectFileNameHelper.BuildBackupFileName(project.ProjectName, DateTime.Now);
        var filePath = Path.Combine(AppStoragePaths.BackupDirectory, fileName);
        _jsonFileStore.Write(filePath, project);
    }
}