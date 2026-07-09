using System.Text.RegularExpressions;

namespace BudsMonitor.Application.Updates;

/// <summary>The kind of a parsed release-notes line.</summary>
public enum ReleaseNotesBlockKind
{
    Heading,
    Bullet,
    Paragraph,
}

/// <summary>An inline run of text with a bold flag.</summary>
public sealed record ReleaseNotesSpan(string Text, bool Bold);

/// <summary>
/// One rendered line: a heading (with level), a bullet (with indent depth), or a paragraph,
/// carrying its inline spans.
/// </summary>
public sealed record ReleaseNotesBlock(
    ReleaseNotesBlockKind Kind,
    int Level,
    IReadOnlyList<ReleaseNotesSpan> Spans);

/// <summary>
/// Minimal GitHub-flavored-markdown parser for the update dialog's release notes — just enough
/// to turn the raw text (headings, bullets, <c>**bold**</c>, inline code) into structured blocks
/// so the UI can render them instead of showing literal <c>##</c> / <c>**</c> markers. Pure and
/// unit-testable; no WPF dependency.
/// </summary>
public static class ReleaseNotesMarkdown
{
    private static readonly Regex HeadingPattern = new(@"^\s*(#{1,6})\s+(.*)$", RegexOptions.Compiled);
    private static readonly Regex BulletPattern = new(@"^(\s*)(?:[-*+]|\d+\.)\s+(.*)$", RegexOptions.Compiled);
    private static readonly Regex BoldPattern = new(@"\*\*(.+?)\*\*", RegexOptions.Compiled);

    public static IReadOnlyList<ReleaseNotesBlock> Parse(string? markdown)
    {
        var blocks = new List<ReleaseNotesBlock>();
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return blocks;
        }

        foreach (var rawLine in markdown.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            var line = rawLine.TrimEnd();
            if (line.Trim().Length == 0)
            {
                continue; // blank lines only affect spacing, which the renderer owns
            }

            var heading = HeadingPattern.Match(line);
            if (heading.Success)
            {
                blocks.Add(new ReleaseNotesBlock(
                    ReleaseNotesBlockKind.Heading,
                    heading.Groups[1].Value.Length,
                    ParseInline(heading.Groups[2].Value)));
                continue;
            }

            var bullet = BulletPattern.Match(line);
            if (bullet.Success)
            {
                var indentText = bullet.Groups[1].Value.Replace("\t", "  ");
                var depth = indentText.Length / 2; // GitHub nests bullets by two spaces
                blocks.Add(new ReleaseNotesBlock(
                    ReleaseNotesBlockKind.Bullet,
                    depth,
                    ParseInline(bullet.Groups[2].Value)));
                continue;
            }

            blocks.Add(new ReleaseNotesBlock(
                ReleaseNotesBlockKind.Paragraph,
                0,
                ParseInline(line.Trim())));
        }

        return blocks;
    }

    private static IReadOnlyList<ReleaseNotesSpan> ParseInline(string text)
    {
        // Render inline code as plain text (drop the backticks); nothing here needs a monospace run.
        text = text.Replace("`", "");

        var spans = new List<ReleaseNotesSpan>();
        var cursor = 0;
        foreach (Match match in BoldPattern.Matches(text))
        {
            if (match.Index > cursor)
            {
                spans.Add(new ReleaseNotesSpan(text[cursor..match.Index], false));
            }

            spans.Add(new ReleaseNotesSpan(match.Groups[1].Value, true));
            cursor = match.Index + match.Length;
        }

        if (cursor < text.Length)
        {
            spans.Add(new ReleaseNotesSpan(text[cursor..], false));
        }

        if (spans.Count == 0)
        {
            spans.Add(new ReleaseNotesSpan(text, false));
        }

        return spans;
    }
}
