using Ai_DailyTracking.Infrastructure;

namespace Ai_DailyTracking.Tests;

public sealed class SharedPathServiceTests
{
    [Fact]
    public void An_old_windows_user_path_uses_the_current_profile_folder()
    {
        using var fixture = new Fixture();
        var suffix = Path.Combine(fixture.UniqueLibrary, "POS_01", "Tareas");
        var expected = Path.Combine(fixture.ProfileA, suffix);
        Directory.CreateDirectory(expected);
        var oldPath = Path.Combine(Path.GetPathRoot(fixture.ProfileA)!, "Users", "otro_usuario", suffix);

        Assert.Equal(expected, fixture.ServiceA.Resolve(oldPath, directory: true));
        Assert.Equal("userprofile:" + suffix.Replace('\\', '/'),
            fixture.ServiceA.ToStoredPath(expected, directory: true));
    }

    [Fact]
    public void A_profile_relative_path_opens_on_another_computer_without_configuring_a_library_root()
    {
        using var fixture = new Fixture();
        var relative = Path.Combine(fixture.UniqueLibrary, "POS_01", "Tareas", "nota.md");
        var firstPath = Path.Combine(fixture.ProfileA, relative);
        var secondPath = Path.Combine(fixture.ProfileB, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(firstPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(secondPath)!);
        File.WriteAllText(firstPath, "A");
        File.WriteAllText(secondPath, "B");

        var stored = fixture.ServiceA.ToStoredPath(firstPath);

        Assert.StartsWith("userprofile:", stored);
        Assert.Equal(secondPath, fixture.ServiceB.Resolve(stored));
    }

    [Fact]
    public void A_selected_root_matches_a_folder_name_and_remembers_the_mapping_for_sibling_files()
    {
        using var fixture = new Fixture();
        var selectedRoot = Path.Combine(fixture.Root, "SelectedRoot");
        var targetFolder = Path.Combine(selectedRoot, "DPTO", "01_PROYECTOS");
        Directory.CreateDirectory(targetFolder);
        var target = Path.Combine(targetFolder, "task.md");
        var sibling = Path.Combine(targetFolder, "other.md");
        File.WriteAllText(target, "A");
        File.WriteAllText(sibling, "B");
        var oldPrefix = Path.Combine(Path.GetPathRoot(fixture.ProfileA)!, "Users", "otro_usuario", fixture.UniqueLibrary);
        var oldPath = Path.Combine(oldPrefix, "DPTO", "01_PROYECTOS", "task.md");

        var match = Assert.Single(fixture.ServiceA.FindMatches(oldPath, selectedRoot));
        Assert.Equal(target, match.ResolvedPath);
        fixture.ServiceA.RememberMatch(match);

        Assert.Equal(target, fixture.ServiceA.Resolve(oldPath));
        Assert.Equal(sibling, fixture.ServiceA.Resolve(Path.Combine(oldPrefix, "DPTO", "01_PROYECTOS", "other.md")));
    }

    [Fact]
    public void The_selected_root_itself_can_match_a_folder_in_the_saved_path()
    {
        using var fixture = new Fixture();
        var selectedRoot = Path.Combine(fixture.Root, "DPTO");
        var target = Path.Combine(selectedRoot, "01_PROYECTOS", "Tareas");
        Directory.CreateDirectory(target);
        var oldPath = Path.Combine(Path.GetPathRoot(fixture.ProfileA)!, "Users", "otro_usuario",
            fixture.UniqueLibrary, "DPTO", "01_PROYECTOS", "Tareas");

        var match = Assert.Single(fixture.ServiceA.FindMatches(oldPath, selectedRoot, directory: true));

        Assert.Equal(target, match.ResolvedPath);
    }

    [Fact]
    public void An_existing_sharepoint_path_can_be_located_without_a_previous_library_root()
    {
        using var fixture = new Fixture();
        var selectedRoot = Path.Combine(fixture.Root, "Library");
        var target = Path.Combine(selectedRoot, "POS_01", "Proyecto", "docs");
        Directory.CreateDirectory(target);
        var stored = "sharepoint:POS_01/Proyecto/docs";

        Assert.Null(fixture.ServiceA.Resolve(stored, directory: true));
        var match = Assert.Single(fixture.ServiceA.FindMatches(stored, selectedRoot, directory: true));
        fixture.ServiceA.RememberMatch(match);

        Assert.Equal(target, fixture.ServiceA.Resolve(stored, directory: true));
    }

    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "AiDailyTrackingPaths-" + Guid.NewGuid().ToString("N"));
        public string UniqueLibrary { get; } = "ACCIONA-Test-" + Guid.NewGuid().ToString("N");
        public string ProfileA { get; }
        public string ProfileB { get; }
        public SharedPathService ServiceA { get; }
        public SharedPathService ServiceB { get; }

        public Fixture()
        {
            ProfileA = Path.Combine(Root, "local-A");
            ProfileB = Path.Combine(Root, "local-B");
            Directory.CreateDirectory(ProfileA);
            Directory.CreateDirectory(ProfileB);
            var json = new JsonFileStore();
            ServiceA = new SharedPathService(new AppPreferencesRepository(json, Path.Combine(Root, "prefs-A.json")), ProfileA);
            ServiceB = new SharedPathService(new AppPreferencesRepository(json, Path.Combine(Root, "prefs-B.json")), ProfileB);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
    }
}
