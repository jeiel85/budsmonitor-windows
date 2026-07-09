using BudsMonitor.Application.Updates;
using Xunit;

namespace BudsMonitor.Tests;

public class ReleaseNotesMarkdownTests
{
    [Fact]
    public void Parse_Heading_StripsHashesAndKeepsLevel()
    {
        var blocks = ReleaseNotesMarkdown.Parse("## What's changed");

        var block = Assert.Single(blocks);
        Assert.Equal(ReleaseNotesBlockKind.Heading, block.Kind);
        Assert.Equal(2, block.Level);
        Assert.Equal("What's changed", Assert.Single(block.Spans).Text);
    }

    [Fact]
    public void Parse_Bullet_WithBold_SplitsInlineSpans()
    {
        var blocks = ReleaseNotesMarkdown.Parse("- **Settings** are wired now.");

        var block = Assert.Single(blocks);
        Assert.Equal(ReleaseNotesBlockKind.Bullet, block.Kind);
        Assert.Equal(2, block.Spans.Count);
        Assert.Equal(new ReleaseNotesSpan("Settings", true), block.Spans[0]);
        Assert.Equal(new ReleaseNotesSpan(" are wired now.", false), block.Spans[1]);
    }

    [Fact]
    public void Parse_NestedBullet_HasIndentDepth()
    {
        var blocks = ReleaseNotesMarkdown.Parse("  - nested item");

        var block = Assert.Single(blocks);
        Assert.Equal(ReleaseNotesBlockKind.Bullet, block.Kind);
        Assert.Equal(1, block.Level);
    }

    [Fact]
    public void Parse_InlineCode_DropsBackticks()
    {
        var blocks = ReleaseNotesMarkdown.Parse("Download `file.zip` now.");

        var block = Assert.Single(blocks);
        Assert.Equal(ReleaseNotesBlockKind.Paragraph, block.Kind);
        Assert.Equal("Download file.zip now.", Assert.Single(block.Spans).Text);
    }

    [Fact]
    public void Parse_SkipsBlankLines_AndKeepsOrder()
    {
        var notes = "## v0.1.4\n\n- first\n\n- second";

        var blocks = ReleaseNotesMarkdown.Parse(notes);

        Assert.Equal(3, blocks.Count);
        Assert.Equal(ReleaseNotesBlockKind.Heading, blocks[0].Kind);
        Assert.Equal("first", blocks[1].Spans[0].Text);
        Assert.Equal("second", blocks[2].Spans[0].Text);
    }

    [Fact]
    public void Parse_OrderedListItem_IsBullet()
    {
        var blocks = ReleaseNotesMarkdown.Parse("1. Download the zip");

        var block = Assert.Single(blocks);
        Assert.Equal(ReleaseNotesBlockKind.Bullet, block.Kind);
        Assert.Equal("Download the zip", block.Spans[0].Text);
    }

    [Fact]
    public void Parse_MultipleBoldSpans_AllMarked()
    {
        var blocks = ReleaseNotesMarkdown.Parse("**A** and **B**");

        var spans = Assert.Single(blocks).Spans;
        Assert.Equal("A", spans[0].Text);
        Assert.True(spans[0].Bold);
        Assert.Equal(" and ", spans[1].Text);
        Assert.False(spans[1].Bold);
        Assert.Equal("B", spans[2].Text);
        Assert.True(spans[2].Bold);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_EmptyOrWhitespace_ReturnsNoBlocks(string? input)
    {
        Assert.Empty(ReleaseNotesMarkdown.Parse(input));
    }
}
