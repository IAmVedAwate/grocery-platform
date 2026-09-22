namespace Domain.Ai;

/// <summary>
/// Metadata for an uploaded knowledge-base document (docs/PRD.md §5.9,
/// §16 RAG). The file bytes live in IStorageService (StorageKey), the
/// searchable content lives as chunks in the vector store
/// (Infrastructure/Ai/DocumentChunk) — this row is neither of those, just
/// the record that ties an upload to a tenant and lets it be listed/deleted.
/// </summary>
public class Document
{
    public Guid Id { get; private set; }
    public Guid StoreId { get; private set; }
    public string FileName { get; private set; } = default!;
    public string ContentType { get; private set; } = default!;
    public string StorageKey { get; private set; } = default!;
    public Guid UploadedByUserId { get; private set; }
    public DateTime UploadedAtUtc { get; private set; }

    private Document() { }

    public Document(Guid storeId, string fileName, string contentType, string storageKey, Guid uploadedByUserId)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("File name is required.", nameof(fileName));
        if (string.IsNullOrWhiteSpace(storageKey))
            throw new ArgumentException("Storage key is required.", nameof(storageKey));

        Id = Guid.NewGuid();
        StoreId = storeId;
        FileName = fileName.Trim();
        ContentType = contentType;
        StorageKey = storageKey;
        UploadedByUserId = uploadedByUserId;
        UploadedAtUtc = DateTime.UtcNow;
    }
}
