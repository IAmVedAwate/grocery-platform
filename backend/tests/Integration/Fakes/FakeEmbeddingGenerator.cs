using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.AI;

namespace Integration.Fakes;

/// <summary>
/// Substituted for the real Gemini-backed embedding generator in every
/// automated test (see CustomWebApplicationFactory) — the whole point is
/// that the RAG storage/retrieval pipeline (chunking, SQL Server native
/// vector upsert, tenant-scoped SearchAsync) is real and worth testing for
/// real, but calling the actual Gemini embeddings API on every test run
/// would burn real tokens for no extra confidence (the "minimum tokens"
/// constraint this pipeline was built under). Deterministic: the same
/// input string always produces the same vector, so cosine similarity
/// between two identical/near-identical chunks is still meaningfully
/// higher than between unrelated ones — enough to test retrieval without
/// needing genuine semantic understanding.
/// </summary>
public sealed class FakeEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    public const int Dimensions = 1536;

    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values, EmbeddingGenerationOptions? options = null, CancellationToken cancellationToken = default)
    {
        var embeddings = values.Select(v => new Embedding<float>(ToVector(v)));
        return Task.FromResult(new GeneratedEmbeddings<Embedding<float>>(embeddings));
    }

    private static float[] ToVector(string input)
    {
        var seedBytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        var seed = BitConverter.ToInt32(seedBytes, 0);
        var random = new Random(seed);

        var vector = new float[Dimensions];
        for (var i = 0; i < Dimensions; i++)
            vector[i] = (float)(random.NextDouble() * 2 - 1);

        return vector;
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }
}
