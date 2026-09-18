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

    // Only switches which folder is read from/written to; does not copy or move any project files.
    public void SetProjectsFolderPath(string newFolderPath)
    {
        var preferences = _preferencesRepository.Load();
        preferences.ProjectsFolderPath = newFolderPath;
        _preferencesRepository.Save(preferences);
    }

    public void SaveBackupCopy(TrackingProject project)
    {
        Directory.CreateDirectory(AppStoragePaths.BackupDirectory);
        var fileName = ProjectFileNameHelper.BuildBackupFileName(project.ProjectName, DateTime.Now);
        var filePath = Path.Combine(AppStoragePaths.BackupDirectory, fileName);
        _jsonFileStore.Write(filePath, project);
    }
}