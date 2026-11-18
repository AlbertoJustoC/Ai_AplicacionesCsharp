using System.Text.RegularExpressions;

namespace Ai_DailyTracking.Shared.Helpers;

public enum NoteLinkKind
{
    Web,
    Email,
    Teams
}

// A link taken from the clipboard (e.g. "Copiar vinculo" in Teams or the URL of a message in Outlook web).
public readonly record struct NoteLink(string Url, NoteLinkKind Kind);

// A "[title](url)" link found inside the notes text. Index/Length cover the whole link; TitleLength covers the
// "[title]" part, so the "(url)" part starts at Index + TitleLength.
public readonly record struct NoteLinkSpan(int Index, int Length, int TitleLength, string Title, string Url);

// Links to an Outlook message or a Teams message live inside the notes as Markdown links, so they follow the
// notes through the project JSON, the shared change log and the .md file with no extra storage.
public static class NoteLinkHelper
{
    // Titles never contain brackets and urls never contain parentheses or spaces (BuildMarkdownLink guarantees it).
    public const string LinkPattern = @"\[(?<title>[^\[\]\r\n]*)\]\((?<url>[^()\s]+)\)";

    private static readonly Regex LinkRegex = new(LinkPattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // Anything else (file:, javascript:, executables...) is refused because notes come from shared files.
    private static readonly string[] AllowedSchemes = ["https", "http", "mailto", "msteams"];
    private static readonly string[] OutlookHosts = ["outlook.cloud.microsoft", "outlook.office.com", "outlook.office365.com", "outlook.live.com"];
    private static readonly string[] TeamsHosts = ["teams.microsoft.com", "teams.cloud.microsoft", "teams.live.com"];

    public static bool IsAllowedUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (!AllowedSchemes.Contains(uri.Scheme, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        var isWebScheme = uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) ||
                          uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase);
        return !isWebScheme || uri.Host.Length > 0;
    }

    public static NoteLinkKind Classify(string url)
    {
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
        {
            return NoteLinkKind.Web;
        }

        if (uri.Scheme.Equals("msteams", StringComparison.OrdinalIgnoreCase) || HostMatches(uri.Host, TeamsHosts))
        {
            return NoteLinkKind.Teams;
        }

        return HostMatches(uri.Host, OutlookHosts) ? NoteLinkKind.Email : NoteLinkKind.Web;
    }

    public static string DefaultTitle(NoteLinkKind kind) => kind switch
    {
        NoteLinkKind.Email => "Correo",
        NoteLinkKind.Teams => "Teams",
        _ => "Enlace"
    };

    public static string DisplayName(NoteLinkKind kind) => kind switch
    {
        NoteLinkKind.Email => "Correo de Outlook",
        NoteLinkKind.Teams => "Mensaje de Teams",
        _ => "Enlace web"
    };

    // Accepts the clipboard text when it holds one allowed link; surrounding text is ignored.
    public static bool TryParseLink(string? clipboardText, out NoteLink link)
    {
        link = default;

        foreach (var token in (clipboardText ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = LinkRegex.Match(token) is { Success: true } markdown ? markdown.Groups["url"].Value : token;

            if (IsAllowedUrl(candidate))
            {
                link = new NoteLink(candidate.Trim(), Classify(candidate));
                return true;
            }
        }

        return false;
    }

    public static string BuildMarkdownLink(string? title, string url)
    {
        var cleanTitle = Regex.Replace(title ?? string.Empty, @"\s+", " ")
            .Replace('[', '(')
            .Replace(']', ')')
            .Trim();

        if (cleanTitle.Length == 0)
        {
            cleanTitle = DefaultTitle(Classify(url));
        }

        var safeUrl = url.Trim()
            .Replace(" ", "%20", StringComparison.Ordinal)
            .Replace("(", "%28", StringComparison.Ordinal)
            .Replace(")", "%29", StringComparison.Ordinal);

        return $"[{cleanTitle}]({safeUrl})";
    }

    public static IReadOnlyList<NoteLinkSpan> FindLinks(string? text)
    {
        return LinkRegex.Matches(text ?? string.Empty)
            .Select(match => new NoteLinkSpan(
                match.Index,
                match.Length,
                match.Groups["title"].Length + 2,
                match.Groups["title"].Value,
                match.Groups["url"].Value))
            .ToList();
    }

    public static NoteLinkSpan? FindLinkAt(string? text, int charIndex)
    {
        foreach (var span in FindLinks(text))
        {
            if (charIndex >= span.Index && charIndex < span.Index + span.Length)
            {
                return span;
            }
        }

        return null;
    }

    private static bool HostMatches(string host, string[] domains)
    {
        return domains.Any(domain =>
            host.Equals(domain, StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase));
    }
}
