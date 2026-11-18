using Ai_DailyTracking.Shared.Helpers;

namespace Ai_DailyTracking.Tests;

public sealed class NoteLinkHelperTests
{
    private const string OutlookUrl = "https://outlook.cloud.microsoft/mail/deeplink/read/AAMkAD%3D?ItemID=abc&exvsurl=1";
    private const string TeamsUrl = "https://teams.microsoft.com/l/message/19%3Aabc%40thread.v2/1700000000000?tenantId=1&createdTime=1700000000000";

    [Theory]
    [InlineData(OutlookUrl, NoteLinkKind.Email)]
    [InlineData(TeamsUrl, NoteLinkKind.Teams)]
    [InlineData("msteams:/l/message/19%3Aabc%40thread.v2/1700000000000", NoteLinkKind.Teams)]
    [InlineData("https://example.com/doc", NoteLinkKind.Web)]
    public void A_link_on_the_clipboard_is_recognised_by_its_kind(string url, NoteLinkKind expectedKind)
    {
        Assert.True(NoteLinkHelper.TryParseLink($"  \r\n{url} \r\n", out var link));

        Assert.Equal(url, link.Url);
        Assert.Equal(expectedKind, link.Kind);
    }

    [Theory]
    [InlineData("")]
    [InlineData("texto sin enlace")]
    [InlineData("file:///C:/Windows/System32/calc.exe")]
    [InlineData("javascript:alert(1)")]
    [InlineData(@"C:\Windows\notepad.exe")]
    [InlineData("ftp://servidor/archivo")]
    public void Anything_that_is_not_a_safe_link_is_refused(string clipboardText)
    {
        Assert.False(NoteLinkHelper.TryParseLink(clipboardText, out _));
        Assert.False(NoteLinkHelper.IsAllowedUrl(clipboardText));
    }

    [Fact]
    public void A_markdown_link_cannot_be_broken_by_brackets_parentheses_or_spaces()
    {
        var markdown = NoteLinkHelper.BuildMarkdownLink("Re: [urgente]", "https://x.com/a(b) c");

        var link = Assert.Single(NoteLinkHelper.FindLinks(markdown));
        Assert.Equal("[Re: (urgente)](https://x.com/a%28b%29%20c)", markdown);
        Assert.Equal("https://x.com/a%28b%29%20c", link.Url);
        Assert.Equal(markdown.Length, link.Length);
    }

    [Fact]
    public void An_empty_title_falls_back_to_the_kind_of_link()
    {
        Assert.Equal($"[Correo]({OutlookUrl})", NoteLinkHelper.BuildMarkdownLink("  ", OutlookUrl));
        Assert.StartsWith("[Teams](", NoteLinkHelper.BuildMarkdownLink(null, TeamsUrl));
    }

    [Fact]
    public void Timestamps_are_not_links_and_a_link_is_found_only_where_it_is()
    {
        var note = $"[2026-11-18 14:00] **Hecho** Ver [Correo]({OutlookUrl}) ok";

        var link = Assert.Single(NoteLinkHelper.FindLinks(note));

        Assert.Equal("Correo", link.Title);
        Assert.Equal(OutlookUrl, link.Url);
        Assert.NotNull(NoteLinkHelper.FindLinkAt(note, link.Index + 2));
        Assert.NotNull(NoteLinkHelper.FindLinkAt(note, link.Index + link.TitleLength + 3));
        Assert.Null(NoteLinkHelper.FindLinkAt(note, 3));
        Assert.Null(NoteLinkHelper.FindLinkAt(note, link.Index + link.Length));
    }

    [Fact]
    public void Printed_notes_show_only_the_link_title()
    {
        var segments = NoteLineParser.Parse($"[2026-11-18 14:00] **Hecho** Ver [Correo]({OutlookUrl}) ok");

        Assert.Equal(
        [
            new NoteSegment(NoteSegmentKind.Timestamp, "[2026-11-18 14:00]"),
            new NoteSegment(NoteSegmentKind.Text, " "),
            new NoteSegment(NoteSegmentKind.Status, "Hecho"),
            new NoteSegment(NoteSegmentKind.Text, " Ver "),
            new NoteSegment(NoteSegmentKind.Link, "Correo"),
            new NoteSegment(NoteSegmentKind.Text, " ok")
        ], segments);
    }
}
