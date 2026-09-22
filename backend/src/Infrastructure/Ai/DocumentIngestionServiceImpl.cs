using Application.Ai;
using Microsoft.Extensions.VectorData;

namespace Infrastructure.Ai;

public sealed class DocumentIngestionServiceImpl(VectorStore vectorStore) : IDocumentIngestionService
{
    private const string CollectionName = "document_chunks";

    public async Task IngestAsync(Guid documentId, Guid storeId, string fileName, string text, CancellationToken ct)
    {
        var collection = vectorStore.GetCollection<string, DocumentChunk>(CollectionName);
        await collection.EnsureCollectionExistsAsync(ct);

        var chunks = TextChunker.Chunk(text);
        var records = chunks.Select((content, index) => new DocumentChunk
        {
            // Deterministic key (documentId:index) rather than a fresh
            // GUID per chunk — re-ingesting the same document (if that
            // ever becomes a feature) would update these rows in place
            // instead of duplicating them, same reasoning the reference
            // project's stable-ID seeding uses.
            Id = $"{documentId}:{index}",
            StoreId = storeId,
            DocumentId = documentId,
            FileName = fileName,
            ChunkIndex = index,
            Content = content,
            Vector = content
        });

        // Upserting a record with an EmbeddingGenerator configured on the
        // collection (Program.cs) calls the real embedding model on
        // Vector's text automatically — nothing here touches a float
        // array directly.
        await collection.UpsertAsync(records, ct);
    }

    public async Task DeleteChunksAsync(Guid documentId, CancellationToken ct)
    {
        var collection = vectorStore.GetCollection<string, DocumentChunk>(CollectionName);
        if (!await collection.CollectionExistsAsync(ct))
            return;

        var keys = new List<string>();
        await foreach (var chunk in collection.GetAsync(c => c.DocumentId == documentId, top: 1000, cancellationToken: ct))
            keys.Add(chunk.Id);

        if (keys.Count > 0)
            await collection.DeleteAsync(keys, ct);
    }
}
