namespace Application.Ai;

/// <summary>
/// The chunk-embed-store half of RAG (docs/PRD.md §16), kept behind this
/// interface so Application never references the vector store SDK
/// directly — same reason IStorageService exists (Application.Common).
/// </summary>
public interface IDocumentIngestionService
{
    Task IngestAsync(Guid documentId, Guid storeId, string fileName, string text, CancellationToken ct);

    Task DeleteChunksAsync(Guid documentId, CancellationToken ct);
}
