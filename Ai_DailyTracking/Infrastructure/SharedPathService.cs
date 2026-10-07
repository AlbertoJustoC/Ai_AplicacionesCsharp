namespace Ai_DailyTracking.Infrastructure;

// The project JSON is shared; this machine's SharePoint sync location is not.
public sealed class SharedPathService
{
    private const string Prefix = "sharepoint:";
    private readonly AppPreferencesRepository _preferencesRepository;

    public SharedPathService(AppPreferencesRepository preferencesRepository)
    {
        _preferencesRepository = preferencesRepository;
    }

    public string? GetLocalRoot() => _preferencesRepository.Load().SharePointLibraryRootPath;

    public void SetLocalRoot(string path)
    {
        var fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        if (!Path.IsPathFullyQualified(fullPath) || !Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException("La raíz de SharePoint no existe en este equipo.");
        }

        var preferences = _preferencesRepository.Load();
        preferences.SharePointLibraryRootPath = fullPath;
        _preferencesRepository.Save(preferences);
    }

    public bool IsWithinLocalRoot(string path)
    {
        var root = GetLocalRoot();
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root) || !Path.IsPathFullyQualified(path))
        {
            return false;
        }

        var relative = Path.GetRelativePath(root, Path.GetFullPath(path));
        return IsSafeRelative(relative);
    }

    public string ToStoredPath(string path, bool directory = false)
    {
        if (path.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return path;
        }

        var localPath = Resolve(path, directory) ?? path;
        var root = GetLocalRoot();
        if (string.IsNullOrWhiteSpace(root) || !IsWithinLocalRoot(localPath))
        {
            return path;
        }

        var relative = Path.GetRelativePath(root, localPath).Replace('\\', '/');
        return Prefix + relative;
    }

    public string? Resolve(string? storedPath, bool directory = false)
    {
        if (string.IsNullOrWhiteSpace(storedPath))
        {
            return null;
        }

        if (storedPath.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            var root = GetLocalRoot();
            var relative = storedPath[Prefix.Length..].Replace('/', Path.DirectorySeparatorChar);
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root) || !IsSafeRelative(relative))
            {
                return null;
            }

            var fullPath = Path.GetFullPath(Path.Combine(root, relative));
            return IsSafeRelative(Path.GetRelativePath(root, fullPath)) ? fullPath : null;
        }

        if (!Path.IsPathFullyQualified(storedPath))
        {
            return null;
        }

        if (directory ? Directory.Exists(storedPath) : File.Exists(storedPath))
        {
            return storedPath;
        }

        // Rebase an old absolute path only when its library segment matches and the target exists.
        var rootPath = GetLocalRoot();
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            return storedPath;
        }

        var libraryName = Path.GetFileName(rootPath);
        var parts = storedPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var index = Array.FindLastIndex(parts, part => string.Equals(part, libraryName, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            return storedPath;
        }

        var candidate = Path.GetFullPath(Path.Combine([rootPath, .. parts[(index + 1)..]]));
        if (!IsSafeRelative(Path.GetRelativePath(rootPath, candidate)))
        {
            return storedPath;
        }

        return (directory ? Directory.Exists(candidate) : File.Exists(candidate)) ? candidate : storedPath;
    }

    private static bool IsSafeRelative(string relative) =>
        relative == "." ||
        (!Path.IsPathRooted(relative) && relative != ".." &&
         !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) &&
         !relative.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal));
}
