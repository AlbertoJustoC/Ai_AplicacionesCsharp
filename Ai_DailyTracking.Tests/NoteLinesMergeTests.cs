using Ai_DailyTracking.Shared.Helpers;

namespace Ai_DailyTracking.Tests;

public sealed class NoteLinesMergeTests
{
    [Fact]
    public void Reordering_lines_is_not_a_change()
    {
        Assert.Empty(NoteLinesMerge.Diff(["A", "B", "C"], ["C", "A", "B"]));
    }

    [Fact]
    public void A_line_edited_in_place_is_one_replacement_not_a_delete_plus_an_add()
    {
        var edit = Assert.Single(NoteLinesMerge.Diff(["A", "B", "C"], ["A", "B editada", "C"]));

        Assert.Equal(new NoteLineEdit(1, 1), edit);
    }

    [Fact]
    public void Deleting_one_line_and_appending_another_stay_separate_changes()
    {
        var edits = NoteLinesMerge.Diff(["A", "B", "C"], ["A", "C", "D"]);

        Assert.Contains(new NoteLineEdit(1, null), edits);
        Assert.Contains(new NoteLineEdit(null, 2), edits);
    }

    [Fact]
    public void A_note_added_on_both_sides_is_kept_once()
    {
        var result = NoteLinesMerge.Merge(["A"], ["A", "B"], ["A", "B"], markdownWins: true);

        Assert.Equal(["A", "B"], result.Lines);
    }
}
