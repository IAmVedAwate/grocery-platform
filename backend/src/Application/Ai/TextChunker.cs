namespace Application.Ai;

/// <summary>
/// Paragraph-aware chunking: greedily packs consecutive paragraphs into a
/// chunk up to MaxChunkSize, hard-splitting any single paragraph that's
/// bigger than that on its own. Pure and dependency-free on purpose — the
/// part of RAG genuinely worth unit-testing directly, no LLM/vector store
/// needed to prove it's correct. No overlap between chunks (a reasonable
/// first-pass simplification, not a hidden gap — noted in
/// docs/architecture/ai-architecture.md).
/// </summary>
public static class TextChunker
{
    public const int MaxChunkSize = 1000;

    public static IReadOnlyList<string> Chunk(string text)
    {
        var paragraphs = text
            .Replace("\r\n", "\n")
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(p => p.Length > 0)
            .ToList();

        var chunks = new List<string>();
        var current = new System.Text.StringBuilder();

        foreach (var paragraph in paragraphs)
        {
            var candidate = paragraph;
            while (candidate.Length > MaxChunkSize)
            {
                // A single paragraph bigger than the whole chunk budget —
                // flush whatever's pending, then hard-split this one on
                // its own, chunk-sized piece at a time.
                if (current.Length > 0)
                {
                    chunks.Add(current.ToString());
                    current.Clear();
                }
                chunks.Add(candidate[..MaxChunkSize]);
                candidate = candidate[MaxChunkSize..];
            }

            var addedLength = current.Length > 0 ? candidate.Length + 2 : candidate.Length;
            if (current.Length > 0 && current.Length + addedLength > MaxChunkSize)
            {
                chunks.Add(current.ToString());
                current.Clear();
            }

            if (current.Length > 0)
                current.Append("\n\n");
            current.Append(candidate);
        }

        if (current.Length > 0)
            chunks.Add(current.ToString());

        return chunks;
    }
}
