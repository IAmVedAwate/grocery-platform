using Google.GenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Ai;

/// <summary>
/// Defers actually resolving the real Gemini-backed embedding generator —
/// and therefore reading GEMINI_API_KEY — until an embedding call is
/// genuinely made, not merely when something holds a reference to one.
///
/// This exists because CommunityToolkit.VectorData.SqlServer's VectorStore
/// eagerly resolves IEmbeddingGenerator the moment VectorStore itself is
/// constructed, and VectorStore is a constructor dependency of every
/// AI-adjacent class in this codebase — including DocumentsController's
/// List/Delete actions, which never actually generate an embedding
/// (GetAsync/DeleteAsync on the collection don't need one; only
/// IngestAsync's UpsertAsync does). Without this wrapper, registering the
/// real generator directly meant GET /api/v1/documents threw 500 with no
/// GEMINI_API_KEY configured — contradicting this app's own "every
/// non-AI-call feature works with no key set" design (see Program.cs).
/// Same "resolve config only at first real use" discipline as every other
/// setting here (DB connection string, JWT key).
/// </summary>
public sealed class LazyGeminiEmbeddingGenerator(IServiceProvider serviceProvider) : IEmbeddingGenerator<string, Embedding<float>>
{
    private IEmbeddingGenerator<string, Embedding<float>>? _inner;

    private IEmbeddingGenerator<string, Embedding<float>> Inner
    {
        get
        {
            if (_inner is not null) return _inner;
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            var embeddingModel = configuration["GEMINI_EMBEDDING_MODEL"] ?? "gemini-embedding-2-preview";
            var dimensions = int.TryParse(configuration["GEMINI_EMBEDDING_DIMENSIONS"], out var d) ? d : 1536;
            _inner = serviceProvider.GetRequiredService<Client>().AsIEmbeddingGenerator(embeddingModel, dimensions);
            return _inner;
        }
    }

    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values, EmbeddingGenerationOptions? options = null, CancellationToken cancellationToken = default)
        => Inner.GenerateAsync(values, options, cancellationToken);

    public object? GetService(Type serviceType, object? serviceKey = null) => Inner.GetService(serviceType, serviceKey);

    public void Dispose() { }
}
