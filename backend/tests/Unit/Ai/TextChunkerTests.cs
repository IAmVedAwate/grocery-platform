using Application.Ai;
using Xunit;

namespace Unit.Ai;

/// <summary>
/// Pure logic, no LLM/vector store/DB needed — exactly the part of RAG
/// that's worth proving with a fast unit test (see TextChunker.cs).
/// </summary>
public class TextChunkerTests
{
    [Fact]
    public void Chunk_EmptyText_ReturnsNoChunks()
    {
        Assert.Empty(TextChunker.Chunk(""));
    }

    [Fact]
    public void Chunk_SingleShortParagraph_ReturnsOneChunkUnchanged()
    {
        var chunks = TextChunker.Chunk("Hello world.");

        Assert.Equal(["Hello world."], chunks);
    }

    [Fact]
    public void Chunk_SeveralShortParagraphs_PacksThemIntoOneChunk()
    {
        var text = "First paragraph.\n\nSecond paragraph.";

        var chunks = TextChunker.Chunk(text);

        Assert.Equal([text], chunks);
    }

    [Fact]
    public void Chunk_ParagraphsExceedingMaxSize_SplitAtAParagraphBoundary_NotMidParagraph()
    {
        var a = new string('A', 400);
        var b = new string('B', 400);
        var c = new string('C', 400);
        var text = string.Join("\n\n", a, b, c);

        var chunks = TextChunker.Chunk(text);

        // A+B fit together (802 chars); adding C would exceed 1000, so C
        // starts a new chunk — never split inside a paragraph.
        Assert.Equal(2, chunks.Count);
        Assert.Equal($"{a}\n\n{b}", chunks[0]);
        Assert.Equal(c, chunks[1]);
        Assert.All(chunks, ch => Assert.True(ch.Length <= TextChunker.MaxChunkSize));
        // No content lost or duplicated across the split.
        Assert.Equal(text, string.Join("\n\n", chunks));
    }

    [Fact]
    public void Chunk_SingleParagraphLargerThanMaxSize_IsHardSplit_WithNoDataLost()
    {
        var text = new string('x', 2500);

        var chunks = TextChunker.Chunk(text);

        Assert.Equal(3, chunks.Count);
        Assert.Equal(1000, chunks[0].Length);
        Assert.Equal(1000, chunks[1].Length);
        Assert.Equal(500, chunks[2].Length);
        // Hard-split pieces are concatenated directly, no separator and
        // no overlap — reassembling them must reproduce the source exactly.
        Assert.Equal(text, string.Concat(chunks));
    }

    [Fact]
    public void Chunk_BlankLinesAndWhitespaceOnlyParagraphs_AreIgnored()
    {
        var chunks = TextChunker.Chunk("First.\n\n\n\n   \n\nSecond.");

        Assert.Equal(["First.\n\nSecond."], chunks);
    }
}
