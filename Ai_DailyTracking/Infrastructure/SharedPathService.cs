namespace Ai_DailyTracking.Infrastructure;

public sealed record SharedPathMatch(string StoredPrefix, string LocalPrefix, string ResolvedPath);

// The project JSON is shared; this machine's SharePoint sync location is not.
public sealed class SharedPathService
{
    private const string Prefix = "sharepoint:";
    private const string UserProfilePrefix = "userprofile:";
    private readonly AppPreferencesRepository _preferencesRepository;
    private readonly string _userProfilePath;

    public SharedPathService(AppPreferencesRepository preferencesRepository, string? userProfilePath = null)
    {
        _preferencesRepository = preferencesRepository;
        _userProfilePath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(userProfilePath ??
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)));
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
        if (string.IsNullOrWhiteSpace(path) || HasPortablePrefix(path))
        {
            return path;
        }

        var localPath = Resolve(path, directory) ?? path;
        if (!Path.IsPathFullyQualified(localPath)) return path;
        localPath = Path.GetFullPath(localPath);
        var root = GetLocalRoot();
        if (!string.IsNullOrWhiteSpace(root) && Directory.Exists(root) && IsWithin(root, localPath))
        {
            return Prefix + PortableRelative(root, localPath);
        }

        foreach (var mapping in ReadMappings().OrderByDescending(item => item.Value.Length))
        {
            if (!Directory.Exists(mapping.Value) || !IsWithin(mapping.Value, localPath)) continue;
            var relative = Path.GetRelativePath(mapping.Value, localPath);
            if (relative == ".") return mapping.Key;
            return HasPortablePrefix(mapping.Key)
                ? mapping.Key.TrimEnd('/') + "/" + relative.Replace('\\', '/')
                : Path.Combine(mapping.Key, relative);
        }

        return IsWithin(_userProfilePath, localPath)
            ? UserProfilePrefix + PortableRelative(_userProfilePath, localPath)
            : path;
    }

    public string? Resolve(string? storedPath, bool directory = false)
    {
        if (string.IsNullOrWhiteSpace(storedPath))
        {
            return null;
        }

        if (storedPath.StartsWith(UserProfilePrefix, StringComparison.OrdinalIgnoreCase))
        {
            var relative = storedPath[UserProfilePrefix.Length..].Replace('/', Path.DirectorySeparatorChar);
            var profilePath = SafeCombine(_userProfilePath, relative);
            if (profilePath is null) return null;
            return TargetExists(profilePath, directory)
                ? profilePath : ResolveMapped(storedPath, directory) ?? profilePath;
        }

        if (storedPath.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            var root = GetLocalRoot();
            var relative = storedPath[Prefix.Length..].Replace('/', Path.DirectorySeparatorChar);
            if (!IsSafeRelative(relative)) return null;
            var sharePointPath = !string.IsNullOrWhiteSpace(root) && Directory.Exists(root)
                ? SafeCombine(root, relative) : null;
            if (sharePointPath is not null && TargetExists(sharePointPath, directory)) return sharePointPath;
            return ResolveMapped(storedPath, directory) ?? sharePointPath;
        }

        if (!Path.IsPathFullyQualified(storedPath))
        {
            return null;
        }

        var fullPath = Path.GetFullPath(storedPath);
        if (TargetExists(fullPath, directory))
        {
            return fullPath;
        }

        var profileCandidate = RebaseWindowsProfile(fullPath);
        if (profileCandidate is not null && TargetExists(profileCandidate, directory)) return profileCandidate;
        var mapped = ResolveMapped(fullPath, directory);
        if (mapped is not null) return mapped;

        // Rebase an old absolute path only when its library segment matches and the target exists.
        var rootPath = GetLocalRoot();
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            return fullPath;
        }

        var libraryName = Path.GetFileName(rootPath);
        var parts = fullPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var index = Array.FindLastIndex(parts, part => string.Equals(part, libraryName, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            return fullPath;
        }

        var suffix = parts[(index + 1)..];
        var candidate = SafeCombine(rootPath, suffix.Length == 0 ? "." : Path.Combine(suffix));
        return candidate is not null && TargetExists(candidate, directory) ? candidate : fullPath;
    }

    public IReadOnlyList<SharedPathMatch> FindMatches(string storedPath, string selectedRoot,
        bool directory = false, bool allowMissingFile = false)
    {
        if (!Directory.Exists(selectedRoot)) return [];
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(selectedRoot));
        var prefix = storedPath.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase) ? Prefix
            : storedPath.StartsWith(UserProfilePrefix, StringComparison.OrdinalIgnoreCase) ? UserProfilePrefix : null;
        var pathRoot = prefix is null && Path.IsPathFullyQualified(storedPath)
            ? Path.GetPathRoot(Path.GetFullPath(storedPath)) : null;
        if (prefix is null && pathRoot is null) return [];
        var remainder = prefix is not null ? storedPath[prefix.Length..] : storedPath[pathRoot!.Length..];
        if (!IsSafeRelative(remainder)) return [];
        var parts = remainder.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return [];

        var matches = new List<SharedPathMatch>();
        var rootName = Path.GetFileName(root);
        for (var index = 0; index < parts.Length; index++)
        {
            if (!directory && index == parts.Length - 1) continue;
            var storedPrefix = prefix is not null
                ? prefix + string.Join('/', parts[..(index + 1)])
                : Path.Combine(pathRoot!, Path.Combine(parts[..(index + 1)]));

            if (string.Equals(rootName, parts[index], StringComparison.OrdinalIgnoreCase)) AddMatch(root);
            var child = Path.Combine(root, parts[index]);
            if (Directory.Exists(child)) AddMatch(child);

            void AddMatch(string localPrefix)
            {
                var suffix = parts[(index + 1)..];
                var target = suffix.Length == 0 ? localPrefix : Path.Combine([localPrefix, .. suffix]);
                var parentExists = !directory && allowMissingFile &&
                    Directory.Exists(Path.GetDirectoryName(target));
                if (!IsWithin(root, target) || !TargetExists(target, directory) && !parentExists) return;
                if (matches.Any(item => string.Equals(item.ResolvedPath, target, StringComparison.OrdinalIgnoreCase))) return;
                matches.Add(new SharedPathMatch(storedPrefix, localPrefix, target));
            }
        }
        return matches;
    }

    public void RememberMatch(SharedPathMatch match)
    {
        if (!Directory.Exists(match.LocalPrefix) || !IsWithin(match.LocalPrefix, match.ResolvedPath))
            throw new DirectoryNotFoundException("La carpeta elegida ya no está disponible.");
        var preferences = _preferencesRepository.Load();
        preferences.SharedPathMappings ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        preferences.SharedPathMappings[match.StoredPrefix] = match.LocalPrefix;
        _preferencesRepository.Save(preferences);
    }

    private string? RebaseWindowsProfile(string fullPath)
    {
        var pathRoot = Path.GetPathRoot(fullPath);
        if (pathRoot is null) return null;
        var parts = fullPath[pathRoot.Length..].Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 || !string.Equals(parts[0], "Users", StringComparison.OrdinalIgnoreCase)) return null;
        return parts.Length == 2 ? _userProfilePath : SafeCombine(_userProfilePath, Path.Combine(parts[2..]));
    }

    private string? ResolveMapped(string storedPath, bool directory)
    {
        var normalized = storedPath.Replace('\\', '/');
        foreach (var mapping in ReadMappings().OrderByDescending(item => item.Key.Length))
        {
            var key = mapping.Key.Replace('\\', '/');
            if (!string.Equals(normalized, key, StringComparison.OrdinalIgnoreCase) &&
                !normalized.StartsWith(key.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase)) continue;
            var suffix = normalized[key.Length..].TrimStart('/');
            var candidate = SafeCombine(mapping.Value, suffix.Replace('/', Path.DirectorySeparatorChar));
            if (candidate is not null && TargetExists(candidate, directory)) return candidate;
        }
        return null;
    }

    private Dictionary<string, string> ReadMappings() =>
        new(_preferencesRepository.Load().SharedPathMappings ?? [], StringComparer.OrdinalIgnoreCase);

    private static bool HasPortablePrefix(string path) =>
        path.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith(UserProfilePrefix, StringComparison.OrdinalIgnoreCase);

    private static string PortableRelative(string root, string path) =>
        Path.GetRelativePath(root, path).Replace('\\', '/');

    private static string? SafeCombine(string root, string relative)
    {
        if (!IsSafeRelative(relative)) return null;
        var combined = Path.GetFullPath(Path.Combine(root, relative));
        return IsWithin(root, combined) ? combined : null;
    }

    private static bool TargetExists(string path, bool directory) =>
        directory ? Directory.Exists(path) : File.Exists(path);

    private static bool IsWithin(string root, string path) =>
        IsSafeRelative(Path.GetRelativePath(root, Path.GetFullPath(path)));

    private static bool IsSafeRelative(string relative) =>
        relative == "." ||
        (!Path.IsPathRooted(relative) && relative != ".." &&
         !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) &&
         !relative.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal));
}
