using Microsoft.Extensions.VectorData;

namespace Infrastructure.Ai;

/// <summary>
/// One row per chunk in the SQL Server native-vector collection (ADR-003).
/// StoreId is marked IsIndexed so VectorSearchOptions.Filter (a real
/// pre-filter combined with the similarity search, not a post-filter on
/// results the client would have to trust) can scope every query to the
/// current tenant before results ever leave the database — see
/// GeminiAiAssistantService's search_documents tool. This is the actual
/// enforcement point PRD §16 means by "retrieval is tenant-scoped."
///
/// Vector holds the chunk's TEXT, not a float array — the collection is
/// configured with an EmbeddingGenerator (Program.cs), so upserting a
/// record calls the real embedding model on whatever string is here.
/// </summary>
public sealed class DocumentChunk
{
    [VectorStoreKey]
    public required string Id { get; set; }

    [VectorStoreData(IsIndexed = true)]
    public required Guid StoreId { get; set; }

    [VectorStoreData]
    public required Guid DocumentId { get; set; }

    [VectorStoreData]
    public required string FileName { get; set; }

    [VectorStoreData]
    public required int ChunkIndex { get; set; }

    [VectorStoreData]
    public required string Content { get; set; }

    [VectorStoreVector(1536)]
    public string? Vector { get; set; }
}
