using Ai_DailyTracking.Domain.Models;
using Ai_DailyTracking.Shared.Constants;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;

namespace Ai_DailyTracking.Infrastructure;

// The original project JSON is an immutable migration baseline. Each save creates a new,
// uniquely named batch in the synced folder; no computer overwrites another computer's batch.
public sealed class ProjectChangeStore
{
    private const string ChangeDirectoryName = ".AiDailyTrackingChanges";
    private const string DeletedKey = "__deleted";
    private const string ProjectNameKey = "__projectname";
    private const string AllowedUsersKey = "__allowedusers";
    private const string FolderPrefixKey = "__folderprefix";
    private const string LastEntryFolderKey = "__lastentryfolder";
    private const string LastMarkdownParentKey = "__lastmarkdownparent";
    private const string CustomOptionsKey = "__customoptions";
    private static readonly string[] PathKeys = ["inputReceivedPath", "markdownFilePath"];

    private readonly JsonFileStore _json;
    private readonly string _workingDirectory;
    private readonly Dictionary<Guid, Snapshot> _editingBases = [];

    public ProjectChangeStore(JsonFileStore json, string? workingDirectory = null)
    {
        _json = json;
        _workingDirectory = workingDirectory ?? Path.Combine(AppStoragePaths.RootDirectory, "Working");
    }

    public IReadOnlyList<TrackingProject> GetAll(string folder)
    {
        var baselines = Directory.GetFiles(folder, "*.json").Select(ReadBaseline)
            .OfType<TrackingProject>().ToList();
        if (baselines.GroupBy(project => project.ProjectId).Any(group => group.Count() > 1))
            throw new InvalidDataException("Hay varios JSON originales con el mismo identificador de proyecto. Retira la copia duplicada antes de fusionar.");
        return baselines.Where(project => !IsDeleted(folder, project.ProjectId))
            .Select(project => Merge(project, folder).Project)
            .OrderByDescending(project => project.UpdatedAtLocal).ToList();
    }

    public ProjectMergeResult? Open(Guid projectId, string folder)
    {
        var baseline = FindBaseline(projectId, folder);
        if (baseline is null || IsDeleted(folder, projectId)) return null;
        var snapshot = Merge(baseline, folder);
        var incoming = snapshot.ChangeIds.Except(ReadSeen(projectId)).Count();
        _editingBases[projectId] = snapshot with { Project = Clone(snapshot.Project) };
        WriteWorkingCopy(snapshot.Project);
        WriteSeen(projectId, snapshot.ChangeIds);
        return ToResult(snapshot, incoming);
    }

    public ProjectMergeResult Create(TrackingProject project, string folder)
    {
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, project.StorageFileName);
        if (File.Exists(path)) throw new IOException($"Ya existe un proyecto llamado {project.StorageFileName}.");
        WriteNewFile(path, _json.Serialize(project));
        var snapshot = Merge(project, folder);
        _editingBases[project.ProjectId] = snapshot with { Project = Clone(snapshot.Project) };
        WriteWorkingCopy(snapshot.Project);
        WriteSeen(project.ProjectId, snapshot.ChangeIds);
        return ToResult(snapshot, 0);
    }

    public ProjectMergeResult Save(TrackingProject edited, string folder)
    {
        var baseline = FindBaseline(edited.ProjectId, folder)
            ?? throw new FileNotFoundException("No se encuentra el JSON original del proyecto.");
        if (IsDeleted(folder, edited.ProjectId)) throw new InvalidOperationException("El proyecto fue eliminado en otro equipo.");
        if (!_editingBases.TryGetValue(edited.ProjectId, out var editingBase))
        {
            throw new InvalidOperationException("Abre el proyecto antes de guardar cambios.");
        }

        var latest = Merge(baseline, folder);
        var incomingCount = latest.ChangeIds.Except(editingBase.ChangeIds).Count();
        var changes = BuildChanges(edited, editingBase);
        if (changes.Count > 0)
        {
            WriteBatch(folder, edited.ProjectId, changes);
            latest = Merge(baseline, folder);
        }

        _editingBases[edited.ProjectId] = latest with { Project = Clone(latest.Project) };
        WriteWorkingCopy(latest.Project);
        WriteSeen(edited.ProjectId, latest.ChangeIds);
        return ToResult(latest, incomingCount);
    }

    public ProjectMergeResult ResolveConflict(Guid projectId, string folder, ProjectFieldConflict conflict, Guid selectedChangeId)
    {
        var baseline = FindBaseline(projectId, folder)
            ?? throw new FileNotFoundException("No se encuentra el JSON original del proyecto.");
        if (IsDeleted(folder, projectId)) throw new InvalidOperationException("El proyecto fue eliminado en otro equipo.");
        var latest = Merge(baseline, folder);
        var current = latest.Conflicts.FirstOrDefault(item => item.EntryId == conflict.EntryId &&
            string.Equals(item.FieldKey, conflict.FieldKey, StringComparison.OrdinalIgnoreCase));
        if (current is null) return ToResult(latest, 0);
        var selected = current.Candidates.FirstOrDefault(item => item.ChangeId == selectedChangeId)
            ?? throw new InvalidOperationException("La versión elegida ya no está entre los conflictos actuales.");
        var now = DateTimeOffset.UtcNow;
        var resolution = new ProjectChange
        {
            ProjectId = projectId,
            EntryId = conflict.EntryId,
            Author = Environment.UserName,
            Machine = Environment.MachineName,
            SavedAtUtc = now,
            ObservedEntryChanges = latest.EntryChangeIds.GetValueOrDefault(conflict.EntryId)?.ToList() ?? [],
            Fields = [new ProjectFieldChange
            {
                Key = conflict.FieldKey,
                Value = selected.Value,
                Supersedes = current.Candidates.Select(item => item.ChangeId).ToList()
            }]
        };
        WriteBatch(folder, projectId, [resolution]);
        latest = Merge(baseline, folder);
        _editingBases[projectId] = latest with { Project = Clone(latest.Project) };
        WriteWorkingCopy(latest.Project);
        WriteSeen(projectId, latest.ChangeIds);
        return ToResult(latest, 0);
    }

    public void DeleteProject(TrackingProject project, string folder)
    {
        var baseline = FindBaseline(project.ProjectId, folder);
        if (baseline is null) return;
        var directory = ChangesPath(folder, project.ProjectId);
        Directory.CreateDirectory(directory);
        var marker = new ProjectChangeBatch
        {
            ProjectId = project.ProjectId, ProjectDeleted = true,
            BaselineHash = BaselineHash(folder, baseline.StorageFileName)
        };
        WriteNewFile(Path.Combine(directory, marker.BatchId.ToString("N") + ".json"), _json.Serialize(marker));
        _editingBases.Remove(project.ProjectId);
        var workingPath = WorkingPath(project.ProjectId);
        if (File.Exists(workingPath)) File.Delete(workingPath);
        var seenPath = SeenPath(project.ProjectId);
        if (File.Exists(seenPath)) File.Delete(seenPath);
    }

    public void Close(Guid? projectId)
    {
        if (projectId is { } id) _editingBases.Remove(id);
    }

    public void UpdateEditingBasePaths(TrackingProject normalized)
    {
        if (!_editingBases.TryGetValue(normalized.ProjectId, out var snapshot)) return;
        var updated = Clone(snapshot.Project);
        updated.LastEntryFolderPath = normalized.LastEntryFolderPath;
        updated.LastMarkdownParentPath = normalized.LastMarkdownParentPath;
        foreach (var entry in updated.Entries)
        {
            var source = normalized.Entries.FirstOrDefault(item => item.EntryId == entry.EntryId);
            if (source is null) continue;
            foreach (var key in PathKeys)
            {
                if (source.Values.TryGetValue(key, out var value)) entry.Values[key] = value;
            }
        }
        _editingBases[normalized.ProjectId] = snapshot with { Project = updated };
        WriteWorkingCopy(normalized);
    }

    private TrackingProject? FindBaseline(Guid projectId, string folder)
    {
        var matches = Directory.GetFiles(folder, "*.json").Select(ReadBaseline)
            .Where(project => project?.ProjectId == projectId).ToList();
        if (matches.Count > 1)
            throw new InvalidDataException("Hay varios JSON originales con el mismo identificador de proyecto.");
        return matches.FirstOrDefault();
    }

    private bool IsDeleted(string folder, Guid projectId) =>
        ReadBatches(folder, projectId).Any(batch => batch.ProjectDeleted);

    private TrackingProject? ReadBaseline(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        if (!document.RootElement.TryGetProperty("projectId", out var id) &&
            !document.RootElement.TryGetProperty("ProjectId", out id)) return null;
        if (id.ValueKind != JsonValueKind.String || !Guid.TryParse(id.GetString(), out var projectId) || projectId == Guid.Empty)
            return null;
        var project = _json.ReadOrDefault<TrackingProject?>(path, null);
        if (project is null) return null;
        project.ProjectId = projectId;
        project.StorageFileName = Path.GetFileName(path);
        project.Entries ??= [];
        var rawEntries = document.RootElement.TryGetProperty("entries", out var entriesJson) ||
            document.RootElement.TryGetProperty("Entries", out entriesJson)
            ? entriesJson : default;
        if (rawEntries.ValueKind == JsonValueKind.Array)
        {
            for (var index = 0; index < Math.Min(project.Entries.Count, rawEntries.GetArrayLength()); index++)
            {
                var entry = project.Entries[index];
                var raw = rawEntries[index];
                if ((!raw.TryGetProperty("entryId", out var rawId) && !raw.TryGetProperty("EntryId", out rawId)) ||
                    rawId.ValueKind != JsonValueKind.String || !Guid.TryParse(rawId.GetString(), out var parsedId) || parsedId == Guid.Empty)
                {
                    var digest = SHA256.HashData(Encoding.UTF8.GetBytes($"{projectId:N}:{index}:legacy-entry"));
                    entry.EntryId = new Guid(digest[..16]);
                }
                if (!raw.TryGetProperty("createdAtLocal", out _) && !raw.TryGetProperty("CreatedAtLocal", out _))
                    entry.CreatedAtLocal = DateTime.MinValue;
                if (!raw.TryGetProperty("updatedAtLocal", out _) && !raw.TryGetProperty("UpdatedAtLocal", out _))
                    entry.UpdatedAtLocal = entry.CreatedAtLocal;
                if (entry.EntryNumber <= 0) entry.EntryNumber = index + 1;
            }
        }
        if (project.Entries.GroupBy(entry => entry.EntryId).Any(group => group.Count() > 1))
            throw new InvalidDataException($"El JSON {Path.GetFileName(path)} contiene registros con el mismo EntryId.");
        return project;
    }

    private List<ProjectChange> BuildChanges(TrackingProject edited, Snapshot editingBase)
    {
        var now = DateTimeOffset.UtcNow;
        var changes = new List<ProjectChange>();
        var oldMetadata = ProjectFields(editingBase.Project);
        var newMetadata = ProjectFields(edited);
        var metadataFields = ChangedFields(Guid.Empty, oldMetadata, newMetadata, editingBase);
        if (metadataFields.Count > 0)
        {
            changes.Add(NewChange(edited.ProjectId, Guid.Empty, now, metadataFields));
        }

        var oldEntries = editingBase.Project.Entries.ToDictionary(entry => entry.EntryId);
        foreach (var entry in edited.Entries)
        {
            oldEntries.TryGetValue(entry.EntryId, out var oldEntry);
            var oldValues = oldEntry?.Values ?? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            var fields = ChangedFields(entry.EntryId, oldValues, entry.Values, editingBase);
            var notes = AddedNotes(oldValues.GetValueOrDefault("notes"), entry.Values.GetValueOrDefault("notes"), now);
            if (oldEntry is null || fields.Count > 0 || notes.Count > 0)
            {
                changes.Add(NewChange(edited.ProjectId, entry.EntryId, now, fields, notes,
                    oldEntry is null ? now : null));
            }
        }

        var editedIds = edited.Entries.Select(entry => entry.EntryId).ToHashSet();
        foreach (var oldEntry in editingBase.Project.Entries.Where(entry => !editedIds.Contains(entry.EntryId)))
        {
            var deletion = NewChange(edited.ProjectId, oldEntry.EntryId, now,
                [new ProjectFieldChange { Key = DeletedKey, Value = "true",
                    Supersedes = HeadsFor(editingBase, oldEntry.EntryId, DeletedKey) }]);
            deletion.ObservedEntryChanges = editingBase.EntryChangeIds.GetValueOrDefault(oldEntry.EntryId)?.ToList() ?? [];
            changes.Add(deletion);
        }
        return changes;
    }

    private static ProjectChange NewChange(Guid projectId, Guid entryId, DateTimeOffset now,
        List<ProjectFieldChange> fields, List<ProjectNoteLine>? notes = null, DateTimeOffset? createdAt = null) =>
        new()
        {
            ProjectId = projectId,
            EntryId = entryId,
            SavedAtUtc = now,
            CreatedAtUtc = createdAt,
            Author = Environment.UserName,
            Machine = Environment.MachineName,
            Fields = fields,
            Notes = notes ?? []
        };

    private static List<ProjectFieldChange> ChangedFields(Guid entryId,
        IReadOnlyDictionary<string, string?> before, IReadOnlyDictionary<string, string?> after, Snapshot editingBase)
    {
        var changes = new List<ProjectFieldChange>();
        foreach (var key in before.Keys.Union(after.Keys, StringComparer.OrdinalIgnoreCase))
        {
            if (string.Equals(key, "notes", StringComparison.OrdinalIgnoreCase)) continue;
            before.TryGetValue(key, out var oldValue);
            after.TryGetValue(key, out var newValue);
            if (string.Equals(oldValue, newValue, StringComparison.Ordinal)) continue;
            changes.Add(new ProjectFieldChange { Key = key, Value = newValue,
                Supersedes = HeadsFor(editingBase, entryId, key) });
        }
        return changes;
    }

    private static List<Guid> HeadsFor(Snapshot snapshot, Guid entryId, string key) =>
        snapshot.Heads.TryGetValue(Address(entryId, key), out var heads)
            ? heads.Select(head => head.ChangeId).ToList()
            : [Guid.Empty];

    private static List<ProjectNoteLine> AddedNotes(string? before, string? after, DateTimeOffset now)
    {
        var remaining = SplitNotes(before).GroupBy(line => line, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var added = new List<ProjectNoteLine>();
        foreach (var line in SplitNotes(after))
        {
            if (remaining.TryGetValue(line, out var count) && count > 0)
            {
                remaining[line] = count - 1;
                continue;
            }
            added.Add(new ProjectNoteLine { Sequence = added.Count, SavedAtUtc = now, Author = Environment.UserName, Text = line });
        }
        return added;
    }

    private Snapshot Merge(TrackingProject baseline, string folder)
    {
        var project = Clone(baseline);
        var batches = ReadBatches(folder, baseline.ProjectId);
        var baselineHash = BaselineHash(folder, baseline.StorageFileName);
        if (batches.Any(batch => !string.Equals(batch.BaselineHash, baselineHash, StringComparison.Ordinal)))
            throw new InvalidDataException("El JSON original del proyecto cambió después de iniciar la fusión. Revisa el archivo antes de continuar; no se han sobrescrito los cambios.");
        if (batches.Any(batch => batch.ProjectDeleted))
            throw new InvalidOperationException("El proyecto fue eliminado en otro equipo.");
        var changes = batches.SelectMany(batch => batch.Changes).ToList();
        if (changes.GroupBy(change => change.ChangeId).Any(group => group.Count() > 1))
            throw new InvalidDataException("El historial contiene identificadores de cambio duplicados.");
        var knownIds = changes.Select(change => change.ChangeId).ToHashSet();
        var entryChangeIds = changes.Where(change => change.EntryId != Guid.Empty)
            .GroupBy(change => change.EntryId)
            .ToDictionary(group => group.Key, group => group.Select(change => change.ChangeId).ToHashSet());
        var baselineEntries = project.Entries
            .OrderBy(entry => entry.EntryNumber)
            .ThenBy(entry => entry.CreatedAtLocal)
            .ThenBy(entry => entry.EntryId).ToList();
        for (var index = 0; index < baselineEntries.Count; index++)
        {
            baselineEntries[index].LegacyOrder = index;
            baselineEntries[index].CreatedAtUtc = null;
        }

        var entries = project.Entries.ToDictionary(entry => entry.EntryId);
        foreach (var group in changes.Where(change => change.EntryId != Guid.Empty && change.CreatedAtUtc is not null)
                     .GroupBy(change => change.EntryId))
        {
            if (entries.ContainsKey(group.Key)) continue;
            var created = group.Min(change => change.CreatedAtUtc)!.Value;
            entries.Add(group.Key, new TrackingEntry
            {
                EntryId = group.Key,
                CreatedAtUtc = created,
                CreatedAtLocal = created.LocalDateTime,
                UpdatedAtLocal = created.LocalDateTime,
                LegacyOrder = null,
                Values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            });
        }

        var baselineFields = new Dictionary<(Guid, string), string?>();
        foreach (var (key, value) in ProjectFields(project)) baselineFields[Address(Guid.Empty, key)] = value;
        foreach (var entry in baseline.Entries)
        {
            foreach (var (key, value) in entry.Values)
            {
                if (!string.Equals(key, "notes", StringComparison.OrdinalIgnoreCase))
                    baselineFields[Address(entry.EntryId, key)] = value;
            }
            baselineFields[Address(entry.EntryId, DeletedKey)] = "false";
        }

        var addresses = baselineFields.Keys.Concat(changes.SelectMany(change => change.Fields.Select(field => Address(change.EntryId, field.Key))))
            .Distinct().ToList();
        var headsByAddress = new Dictionary<(Guid, string), List<ProjectFieldCandidate>>();
        var conflicts = new List<ProjectFieldConflict>();
        var deleted = new HashSet<Guid>();
        foreach (var address in addresses)
        {
            var revisions = new List<(ProjectFieldCandidate Candidate, ProjectFieldChange? Edit)>();
            if (address.Item1 == Guid.Empty || baseline.Entries.Any(entry => entry.EntryId == address.Item1))
            {
                baselineFields.TryGetValue(address, out var value);
                revisions.Add((new ProjectFieldCandidate(Guid.Empty, value, "Desconocido", string.Empty, DateTimeOffset.MinValue), null));
            }
            revisions.AddRange(changes.Where(change => change.EntryId == address.Item1)
                .SelectMany(change => change.Fields.Where(field => Address(change.EntryId, field.Key) == address)
                    .Select(field => (new ProjectFieldCandidate(change.ChangeId, field.Value, change.Author, change.Machine, change.SavedAtUtc), (ProjectFieldChange?)field))));
            var superseded = revisions.SelectMany(item => item.Edit?.Supersedes ?? []).ToHashSet();
            var heads = revisions.Select(item => item.Candidate).Where(candidate => !superseded.Contains(candidate.ChangeId))
                .OrderByDescending(candidate => candidate.SavedAtUtc).ThenByDescending(candidate => candidate.ChangeId).ToList();
            if (heads.Count == 0) throw new InvalidDataException("El historial de cambios contiene una referencia circular o incompleta.");
            headsByAddress[address] = heads;
            var winner = heads[0];
            if (address.Item1 == Guid.Empty)
            {
                ApplyProjectField(project, address.Item2, winner.Value);
            }
            else if (address.Item2 == DeletedKey)
            {
                if (winner.Value == "true") deleted.Add(address.Item1);
            }
            else if (entries.TryGetValue(address.Item1, out var entry))
            {
                entry.Values[address.Item2] = winner.Value;
                if (winner.SavedAtUtc != DateTimeOffset.MinValue)
                    entry.FieldModifiedAtUtc[address.Item2] = winner.SavedAtUtc;
            }

            if (address.Item1 != Guid.Empty && heads.Select(head => head.Value).Distinct(StringComparer.Ordinal).Count() > 1 &&
                !PathKeys.Contains(address.Item2, StringComparer.OrdinalIgnoreCase))
            {
                conflicts.Add(new ProjectFieldConflict(address.Item1,
                    entries.TryGetValue(address.Item1, out var conflictEntry) ? conflictEntry.EntryNumber : 0,
                    address.Item2, heads));
            }
        }

        foreach (var entryId in entries.Keys)
        {
            if (conflicts.Any(conflict => conflict.EntryId == entryId && conflict.FieldKey == DeletedKey)) continue;
            if (!headsByAddress.TryGetValue(Address(entryId, DeletedKey), out var deleteHeads)) continue;
            foreach (var head in deleteHeads.Where(candidate => candidate.Value == "true" && candidate.ChangeId != Guid.Empty))
            {
                var deletion = changes.First(change => change.ChangeId == head.ChangeId);
                var unobserved = changes.Where(change => change.EntryId == entryId &&
                    change.ChangeId != deletion.ChangeId && !deletion.ObservedEntryChanges.Contains(change.ChangeId) &&
                    (change.CreatedAtUtc is not null || change.Notes.Count > 0 ||
                     change.Fields.Any(field => !string.Equals(field.Key, DeletedKey, StringComparison.OrdinalIgnoreCase))))
                    .OrderByDescending(change => change.SavedAtUtc).ThenByDescending(change => change.ChangeId).ToList();
                if (unobserved.Count == 0) continue;
                var candidates = new List<ProjectFieldCandidate> { head };
                candidates.AddRange(unobserved.Select(change => new ProjectFieldCandidate(change.ChangeId,
                    "false", change.Author, change.Machine, change.SavedAtUtc)));
                conflicts.Add(new ProjectFieldConflict(entryId, entries[entryId].EntryNumber, DeletedKey, candidates));
            }
        }

        foreach (var entry in entries.Values)
        {
            var notes = new List<(int LegacyIndex, ProjectNoteLine Line)>();
            var baselineEntry = baseline.Entries.FirstOrDefault(item => item.EntryId == entry.EntryId);
            if (baselineEntry is not null)
            {
                notes.AddRange(SplitNotes(baselineEntry.Values.GetValueOrDefault("notes"))
                    .Select((text, index) => (index, new ProjectNoteLine
                    {
                        NoteId = Guid.Empty, SavedAtUtc = DateTimeOffset.MinValue,
                        Author = "Desconocido", Text = text
                    })));
            }
            notes.AddRange(changes.Where(change => change.EntryId == entry.EntryId)
                .SelectMany(change => change.Notes).GroupBy(line => line.NoteId).Select(group => group.First())
                .Select(line => (int.MaxValue, line)));
            var ordered = notes.OrderBy(item => item.LegacyIndex).ThenBy(item => item.Line.SavedAtUtc)
                .ThenBy(item => item.Line.Sequence).ThenBy(item => item.Line.NoteId)
                .Select(item => item.Line.Text).ToList();
            entry.Values["notes"] = ordered.Count == 0 ? null : string.Join(Environment.NewLine, ordered);
            var latestNote = notes.Select(item => (DateTimeOffset?)item.Line.SavedAtUtc).Max();
            if (latestNote is not null && latestNote != DateTimeOffset.MinValue)
                entry.FieldModifiedAtUtc["notes"] = latestNote.Value;
            var mostRecent = changes.Where(change => change.EntryId == entry.EntryId)
                .Select(change => (DateTimeOffset?)change.SavedAtUtc).Max();
            if (mostRecent is not null) entry.UpdatedAtLocal = mostRecent.Value.LocalDateTime;
        }

        project.Entries = entries.Values.Where(entry => !deleted.Contains(entry.EntryId))
            .OrderBy(entry => entry.LegacyOrder is null)
            .ThenBy(entry => entry.LegacyOrder ?? int.MaxValue)
            .ThenBy(entry => entry.CreatedAtUtc ?? DateTimeOffset.MaxValue)
            .ThenBy(entry => entry.EntryId).ToList();
        for (var index = 0; index < project.Entries.Count; index++) project.Entries[index].EntryNumber = index + 1;
        if (changes.Count > 0) project.UpdatedAtLocal = changes.Max(change => change.SavedAtUtc).LocalDateTime;
        return new Snapshot(project, headsByAddress, conflicts, knownIds, entryChangeIds);
    }

    private static (Guid, string) Address(Guid entryId, string key) => (entryId, key.ToLowerInvariant());

    private static Dictionary<string, string?> ProjectFields(TrackingProject project) => new(StringComparer.OrdinalIgnoreCase)
    {
        [ProjectNameKey] = project.ProjectName,
        [AllowedUsersKey] = JsonSerializer.Serialize(project.AllowedUserNames),
        [FolderPrefixKey] = project.FolderNamePrefix,
        [LastEntryFolderKey] = project.LastEntryFolderPath,
        [LastMarkdownParentKey] = project.LastMarkdownParentPath,
        [CustomOptionsKey] = JsonSerializer.Serialize(project.CustomFieldOptions)
    };

    private static void ApplyProjectField(TrackingProject project, string key, string? value)
    {
        switch (key)
        {
            case ProjectNameKey: project.ProjectName = value ?? string.Empty; break;
            case AllowedUsersKey: project.AllowedUserNames = JsonSerializer.Deserialize<List<string>>(value ?? "[]") ?? []; break;
            case FolderPrefixKey: project.FolderNamePrefix = value; break;
            case LastEntryFolderKey: project.LastEntryFolderPath = value; break;
            case LastMarkdownParentKey: project.LastMarkdownParentPath = value; break;
            case CustomOptionsKey: project.CustomFieldOptions = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(value ?? "{}") ?? new(StringComparer.OrdinalIgnoreCase); break;
        }
    }

    private static List<string> SplitNotes(string? text) => (text ?? string.Empty)
        .Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n')
        .Select(line => line.TrimEnd('\r')).Where(line => !string.IsNullOrWhiteSpace(line)).ToList();

    private IReadOnlyList<ProjectChangeBatch> ReadBatches(string folder, Guid projectId)
    {
        var directory = ChangesPath(folder, projectId);
        if (!Directory.Exists(directory)) return [];
        var batches = new List<ProjectChangeBatch>();
        var seen = new Dictionary<Guid, string>();
        foreach (var path in Directory.GetFiles(directory, "*.json"))
        {
            var raw = ReadSyncedChange(path);
            var batch = _json.Deserialize<ProjectChangeBatch?>(raw, null)
                ?? throw new InvalidDataException($"Cambio vacío: {path}");
            if (batch.ProjectId != projectId || batch.BatchId == Guid.Empty || batch.Changes is null ||
                batch.Changes.Any(change => change is null || change.ProjectId != projectId || change.ChangeId == Guid.Empty ||
                    change.Fields is null || change.Notes is null || change.ObservedEntryChanges is null ||
                    change.Fields.Any(field => field is null || string.IsNullOrWhiteSpace(field.Key) || field.Supersedes is null) ||
                    change.Notes.Any(note => note is null || note.NoteId == Guid.Empty || note.Text is null)))
                throw new InvalidDataException($"Cambio incompatible: {path}");
            if (seen.TryGetValue(batch.BatchId, out var previous))
            {
                if (!string.Equals(previous, raw, StringComparison.Ordinal))
                    throw new InvalidDataException($"Dos archivos distintos comparten el identificador {batch.BatchId}.");
                continue;
            }
            seen.Add(batch.BatchId, raw);
            batches.Add(batch);
        }
        return batches;
    }

    private static string ReadSyncedChange(string path)
    {
        const int attempts = 3;
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                return File.ReadAllText(path);
            }
            catch (IOException ex) when (ex.HResult == unchecked((int)0x8007017C) ||
                                         ex.HResult == unchecked((int)0x8007016A))
            {
                if (attempt < attempts)
                {
                    System.Threading.Thread.Sleep(500);
                    continue;
                }

                throw new IOException(
                    $"OneDrive no permite leer este archivo de cambios: {path}{Environment.NewLine}{Environment.NewLine}" +
                    "Abre la carpeta en el Explorador y marca .AiDailyTrackingChanges como 'Mantener siempre en este dispositivo'. " +
                    "Espera a que termine la sincronización y vuelve a abrir el proyecto. " +
                    "La aplicación no omite este archivo porque contiene cambios de otro usuario.", ex);
            }
        }

        throw new InvalidOperationException("No se pudo completar la lectura del cambio sincronizado.");
    }

    private void WriteBatch(string folder, Guid projectId, List<ProjectChange> changes)
    {
        var baseline = FindBaseline(projectId, folder)
            ?? throw new FileNotFoundException("No se encuentra el JSON original del proyecto.");
        BackupBaseline(folder, baseline);
        var batch = new ProjectChangeBatch
        {
            ProjectId = projectId, Changes = changes,
            BaselineHash = BaselineHash(folder, baseline.StorageFileName)
        };
        var directory = ChangesPath(folder, projectId);
        Directory.CreateDirectory(directory);
        WriteNewFile(Path.Combine(directory, batch.BatchId.ToString("N") + ".json"), _json.Serialize(batch));
    }

    private static void WriteNewFile(string path, string content)
    {
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, content, Encoding.UTF8);
            File.Move(temporary, path, overwrite: false);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private void WriteWorkingCopy(TrackingProject project)
    {
        Directory.CreateDirectory(_workingDirectory);
        var path = WorkingPath(project.ProjectId);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, _json.Serialize(project), Encoding.UTF8);
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private string WorkingPath(Guid projectId) => Path.Combine(_workingDirectory, projectId.ToString("N") + ".json");
    private string SeenPath(Guid projectId) => Path.Combine(_workingDirectory, projectId.ToString("N") + ".seen.json");
    private HashSet<Guid> ReadSeen(Guid projectId) => _json.ReadOrDefault(SeenPath(projectId), new List<Guid>()).ToHashSet();
    private void WriteSeen(Guid projectId, HashSet<Guid> ids) => _json.Write(SeenPath(projectId), ids.ToList());
    private static string ChangesPath(string folder, Guid projectId) =>
        Path.Combine(folder, ChangeDirectoryName, projectId.ToString("N"));

    private static string BaselineHash(string folder, string fileName)
    {
        var bytes = File.ReadAllBytes(Path.Combine(folder, fileName));
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    private void BackupBaseline(string folder, TrackingProject baseline)
    {
        var hash = BaselineHash(folder, baseline.StorageFileName);
        var backupDirectory = Path.Combine(_workingDirectory, "BaselineBackups");
        Directory.CreateDirectory(backupDirectory);
        var backupPath = Path.Combine(backupDirectory, $"{baseline.ProjectId:N}-{hash}.json");
        if (!File.Exists(backupPath))
        {
            try { File.Copy(Path.Combine(folder, baseline.StorageFileName), backupPath, overwrite: false); }
            catch (IOException) when (File.Exists(backupPath)) { }
        }
    }

    private TrackingProject Clone(TrackingProject project) => _json.Deserialize<TrackingProject?>(_json.Serialize(project), null)!;
    private static ProjectMergeResult ToResult(Snapshot snapshot, int incorporated) =>
        new(snapshot.Project, snapshot.Conflicts, incorporated);

    private sealed record Snapshot(TrackingProject Project,
        Dictionary<(Guid, string), List<ProjectFieldCandidate>> Heads,
        List<ProjectFieldConflict> Conflicts, HashSet<Guid> ChangeIds,
        Dictionary<Guid, HashSet<Guid>> EntryChangeIds);
}
