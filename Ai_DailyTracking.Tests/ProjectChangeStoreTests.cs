using Ai_DailyTracking.Domain.Models;
using Ai_DailyTracking.Infrastructure;

namespace Ai_DailyTracking.Tests;

public sealed class ProjectChangeStoreTests
{
    [Fact]
    public void Two_new_entries_with_the_same_provisional_number_are_both_kept_and_renumbered()
    {
        using var fixture = new Fixture();
        var original = fixture.First.Create(fixture.NewProject(), fixture.SharedFolder).Project;
        var other = fixture.Second.Open(original.ProjectId, fixture.SharedFolder)!.Project;
        original.Entries.Add(fixture.NewEntry("A", 1));
        other.Entries.Add(fixture.NewEntry("B", 1));

        fixture.First.Save(original, fixture.SharedFolder);
        fixture.Second.Save(other, fixture.SharedFolder);
        var merged = fixture.First.Open(original.ProjectId, fixture.SharedFolder)!.Project;

        Assert.Equal(2, merged.Entries.Count);
        Assert.Equal([1, 2], merged.Entries.Select(entry => entry.EntryNumber));
        Assert.Equal(2, merged.Entries.Select(entry => entry.EntryId).Distinct().Count());
        var otherView = fixture.Second.Open(original.ProjectId, fixture.SharedFolder)!.Project;
        Assert.Equal(merged.Entries.Select(entry => (entry.EntryId, entry.EntryNumber)),
            otherView.Entries.Select(entry => (entry.EntryId, entry.EntryNumber)));
    }

    [Fact]
    public void Concurrent_edits_to_one_field_wait_for_a_user_choice()
    {
        using var fixture = new Fixture();
        var initial = fixture.NewProject(fixture.NewEntry("Original", 1));
        var first = fixture.First.Create(initial, fixture.SharedFolder).Project;
        var second = fixture.Second.Open(initial.ProjectId, fixture.SharedFolder)!.Project;
        first.Entries[0].Values["status"] = "En curso";
        second.Entries[0].Values["status"] = "Cerrado";

        fixture.First.Save(first, fixture.SharedFolder);
        var competing = fixture.Second.Save(second, fixture.SharedFolder);
        var conflict = Assert.Single(competing.Conflicts.Where(item => item.FieldKey == "status"));
        Assert.Equal(2, conflict.Candidates.Count);
        var selected = Assert.Single(conflict.Candidates.Where(item => item.Value == "En curso"));

        var resolved = fixture.Second.ResolveConflict(initial.ProjectId, fixture.SharedFolder, conflict, selected.ChangeId);
        Assert.Empty(resolved.Conflicts);
        Assert.Equal("En curso", resolved.Project.Entries[0].Values["status"]);
    }

    [Fact]
    public void Distinct_fields_merge_notes_accumulate_and_the_newest_path_wins()
    {
        using var fixture = new Fixture();
        var initial = fixture.NewProject(fixture.NewEntry("Original", 1));
        initial.Entries[0].Values["notes"] = "Nota antigua";
        var first = fixture.First.Create(initial, fixture.SharedFolder).Project;
        var second = fixture.Second.Open(initial.ProjectId, fixture.SharedFolder)!.Project;
        first.Entries[0].Values["status"] = "En curso";
        first.Entries[0].Values["inputReceivedPath"] = "sharepoint:Carpeta-A";
        first.Entries[0].Values["notes"] = "Nota antigua\nNota A";
        second.Entries[0].Values["priority"] = "Alta";
        second.Entries[0].Values["inputReceivedPath"] = "sharepoint:Carpeta-B";
        second.Entries[0].Values["notes"] = "Nota antigua\nNota B";

        fixture.First.Save(first, fixture.SharedFolder);
        Thread.Sleep(20);
        var merged = fixture.Second.Save(second, fixture.SharedFolder);

        Assert.Empty(merged.Conflicts);
        var entry = Assert.Single(merged.Project.Entries);
        Assert.Equal("En curso", entry.Values["status"]);
        Assert.Equal("Alta", entry.Values["priority"]);
        Assert.Equal("sharepoint:Carpeta-B", entry.Values["inputReceivedPath"]);
        Assert.True(entry.FieldModifiedAtUtc["inputReceivedPath"] > DateTimeOffset.MinValue);
        Assert.Contains("Nota A", entry.Values["notes"]);
        Assert.Contains("Nota B", entry.Values["notes"]);
        Assert.Contains("Nota antigua", entry.Values["notes"]);
    }

    [Fact]
    public void Legacy_order_is_preserved_ahead_of_new_entries_without_inventing_creation_times()
    {
        using var fixture = new Fixture();
        var older = fixture.NewEntry("Older label", 2);
        var first = fixture.NewEntry("First label", 1);
        var initial = fixture.NewProject(older, first);
        var opened = fixture.First.Create(initial, fixture.SharedFolder).Project;
        opened.Entries.Add(fixture.NewEntry("New", 1));

        var merged = fixture.First.Save(opened, fixture.SharedFolder).Project;

        Assert.Equal(["First label", "Older label", "New"],
            merged.Entries.Select(entry => entry.Values["activity"]));
        Assert.Equal([1, 2, 3], merged.Entries.Select(entry => entry.EntryNumber));
        Assert.Null(merged.Entries[0].CreatedAtUtc);
        Assert.NotNull(merged.Entries[2].CreatedAtUtc);
    }

    [Fact]
    public void Legacy_entries_with_the_same_number_follow_their_creation_time()
    {
        using var fixture = new Fixture();
        var later = fixture.NewEntry("Later", 4);
        later.CreatedAtLocal = new DateTime(2024, 5, 2, 11, 0, 0);
        var earlier = fixture.NewEntry("Earlier", 4);
        earlier.CreatedAtLocal = new DateTime(2024, 5, 2, 9, 0, 0);
        var project = fixture.First.Create(fixture.NewProject(later, earlier), fixture.SharedFolder).Project;

        Assert.Equal(["Earlier", "Later"], project.Entries.Select(entry => entry.Values["activity"]));
        Assert.Equal([1, 2], project.Entries.Select(entry => entry.EntryNumber));
    }

    [Fact]
    public void Project_settings_are_rebuilt_and_a_deletion_marker_hides_the_project()
    {
        using var fixture = new Fixture();
        var project = fixture.First.Create(fixture.NewProject(), fixture.SharedFolder).Project;
        project.FolderNamePrefix = "NLD";
        project.AllowedUserNames = ["ana"];
        fixture.First.Save(project, fixture.SharedFolder);

        var opened = fixture.Second.Open(project.ProjectId, fixture.SharedFolder)!.Project;
        Assert.Equal("NLD", opened.FolderNamePrefix);
        Assert.Equal(["ana"], opened.AllowedUserNames);

        fixture.First.DeleteProject(project, fixture.SharedFolder);
        Assert.Null(fixture.Second.Open(project.ProjectId, fixture.SharedFolder));
        Assert.Empty(fixture.Second.GetAll(fixture.SharedFolder));
    }

    [Fact]
    public void Incoming_changes_are_reported_once_per_computer()
    {
        using var fixture = new Fixture();
        var project = fixture.First.Create(fixture.NewProject(fixture.NewEntry("Original", 1)), fixture.SharedFolder).Project;
        fixture.Second.Open(project.ProjectId, fixture.SharedFolder);
        project.Entries[0].Values["status"] = "En curso";
        fixture.First.Save(project, fixture.SharedFolder);

        Assert.Equal(1, fixture.Second.Open(project.ProjectId, fixture.SharedFolder)!.IncorporatedChanges);
        Assert.Equal(0, fixture.Second.Open(project.ProjectId, fixture.SharedFolder)!.IncorporatedChanges);
    }

    [Fact]
    public void A_legacy_writer_changing_the_baseline_stops_fusion()
    {
        using var fixture = new Fixture();
        var project = fixture.First.Create(fixture.NewProject(fixture.NewEntry("Original", 1)), fixture.SharedFolder).Project;
        project.Entries[0].Values["status"] = "En curso";
        fixture.First.Save(project, fixture.SharedFolder);
        var path = Path.Combine(fixture.SharedFolder, project.StorageFileName);
        var json = new JsonFileStore();
        var legacy = json.ReadOrDefault<TrackingProject?>(path, null)!;
        legacy.ProjectName = "Cambio desde versión antigua";
        json.Write(path, legacy);

        Assert.Throws<InvalidDataException>(() => fixture.Second.Open(project.ProjectId, fixture.SharedFolder));
    }

    [Fact]
    public void Clearing_a_path_is_a_dated_change_that_beats_an_older_path()
    {
        using var fixture = new Fixture();
        var initial = fixture.NewEntry("Original", 1);
        initial.Values["markdownFilePath"] = "sharepoint:old.md";
        var project = fixture.First.Create(fixture.NewProject(initial), fixture.SharedFolder).Project;
        var second = fixture.Second.Open(project.ProjectId, fixture.SharedFolder)!.Project;
        project.Entries[0].Values["markdownFilePath"] = "sharepoint:new.md";
        fixture.First.Save(project, fixture.SharedFolder);
        Thread.Sleep(20);
        second.Entries[0].Values["markdownFilePath"] = null;

        var merged = fixture.Second.Save(second, fixture.SharedFolder);

        Assert.Empty(merged.Conflicts);
        Assert.Null(merged.Project.Entries[0].Values["markdownFilePath"]);
        Assert.True(merged.Project.Entries[0].FieldModifiedAtUtc["markdownFilePath"] > DateTimeOffset.MinValue);
    }

    [Fact]
    public void Notes_added_in_one_save_keep_their_written_order()
    {
        using var fixture = new Fixture();
        var project = fixture.First.Create(fixture.NewProject(fixture.NewEntry("Original", 1)), fixture.SharedFolder).Project;
        project.Entries[0].Values["notes"] = "Primera\nSegunda\nTercera";

        var saved = fixture.First.Save(project, fixture.SharedFolder).Project;

        Assert.Equal(["Primera", "Segunda", "Tercera"],
            saved.Entries[0].Values["notes"]!.Split(Environment.NewLine));
    }

    [Fact]
    public void A_delete_competing_with_an_unseen_edit_requires_a_choice()
    {
        using var fixture = new Fixture();
        var initial = fixture.NewProject(fixture.NewEntry("Original", 1));
        var deleting = fixture.First.Create(initial, fixture.SharedFolder).Project;
        var editing = fixture.Second.Open(initial.ProjectId, fixture.SharedFolder)!.Project;
        deleting.Entries.Clear();
        fixture.First.Save(deleting, fixture.SharedFolder);
        editing.Entries[0].Values["status"] = "En curso";

        var merged = fixture.Second.Save(editing, fixture.SharedFolder);
        var conflict = Assert.Single(merged.Conflicts.Where(item => item.FieldKey == "__deleted"));
        var keep = Assert.Single(conflict.Candidates.Where(item => item.Value == "false"));
        var resolved = fixture.Second.ResolveConflict(initial.ProjectId, fixture.SharedFolder, conflict, keep.ChangeId);

        Assert.Empty(resolved.Conflicts);
        Assert.Equal("En curso", Assert.Single(resolved.Project.Entries).Values["status"]);
    }

    [Fact]
    public void Older_json_without_entry_id_or_creation_time_gets_a_stable_identity_without_a_fake_time()
    {
        using var fixture = new Fixture();
        var projectId = Guid.NewGuid();
        var path = Path.Combine(fixture.SharedFolder, "legacy.json");
        File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(new
        {
            projectId,
            projectName = "Legacy",
            entries = new[] { new { entryNumber = 1, values = new { activity = "A" } } }
        }));

        var first = Assert.Single(fixture.First.Open(projectId, fixture.SharedFolder)!.Project.Entries);
        var second = Assert.Single(fixture.Second.Open(projectId, fixture.SharedFolder)!.Project.Entries);

        Assert.Equal(first.EntryId, second.EntryId);
        Assert.Equal(DateTime.MinValue, first.CreatedAtLocal);
        Assert.Null(first.CreatedAtUtc);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "AiDailyTrackingChanges-" + Guid.NewGuid().ToString("N"));
        private readonly JsonFileStore _json = new();
        public string SharedFolder { get; }
        public ProjectChangeStore First { get; }
        public ProjectChangeStore Second { get; }

        public Fixture()
        {
            SharedFolder = Path.Combine(_root, "Shared");
            Directory.CreateDirectory(SharedFolder);
            First = new ProjectChangeStore(_json, Path.Combine(_root, "PC1"));
            Second = new ProjectChangeStore(_json, Path.Combine(_root, "PC2"));
        }

        public TrackingProject NewProject(params TrackingEntry[] entries) => new()
        {
            ProjectId = Guid.NewGuid(), ProjectName = "Proyecto",
            StorageFileName = Guid.NewGuid().ToString("N") + ".json",
            Entries = entries.ToList()
        };

        public TrackingEntry NewEntry(string activity, int number) => new()
        {
            EntryId = Guid.NewGuid(), EntryNumber = number,
            Values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["activity"] = activity,
                ["status"] = "Nuevo",
                ["priority"] = "Media"
            }
        };

        public void Dispose()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }
    }
}
