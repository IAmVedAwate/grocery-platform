using Application.Common;
using Domain.Ai;
using Shared.Exceptions;

namespace Application.Ai;

/// <summary>
/// Plain text and Markdown only for now (.txt/.md) — real text extraction
/// (PDF, DOCX) is a real, separate piece of work (layout parsing, OCR for
/// scanned pages) that would need its own library and its own scope, not
/// something to fold in silently here. This still proves the whole
/// pipeline — upload, chunk, embed, retrieve, cite — end to end.
/// </summary>
public sealed class DocumentApplicationService(
    IDocumentRepository documents,
    IStorageService storage,
    IDocumentIngestionService ingestion,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork,
    IAuditWriter auditWriter)
{
    private static readonly IReadOnlySet<string> AllowedContentTypes = new HashSet<string> { "text/plain", "text/markdown" };
    private const long MaxDocumentBytes = 2 * 1024 * 1024;

    public async Task<Document> UploadAsync(Stream content, string fileName, string contentType, long contentLength, CancellationToken ct)
    {
        if (!AllowedContentTypes.Contains(contentType))
            throw new ValidationAppException(new Dictionary<string, string[]>
            {
                ["file"] = [$"Unsupported document type '{contentType}'. Allowed: {string.Join(", ", AllowedContentTypes)} (.txt/.md)."]
            });
        if (contentLength > MaxDocumentBytes)
            throw new ValidationAppException(new Dictionary<string, string[]>
            {
                ["file"] = [$"Document exceeds the {MaxDocumentBytes / 1024 / 1024}MB limit."]
            });

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        buffer.Position = 0;
        var text = await new StreamReader(buffer).ReadToEndAsync(ct);
        if (string.IsNullOrWhiteSpace(text))
            throw new ValidationAppException(new Dictionary<string, string[]> { ["file"] = ["Document is empty."] });

        buffer.Position = 0;
        var storageKey = await storage.SaveAsync(buffer, Path.GetExtension(fileName), ct);

        var document = new Document(tenantContext.StoreId, fileName, contentType, storageKey, tenantContext.UserId);
        await documents.AddAsync(document, ct);
        await unitOfWork.SaveChangesAsync(ct);

        // Ingestion happens after the row (and its Id) is safely committed —
        // chunks reference documentId as their own scoping/citation key.
        await ingestion.IngestAsync(document.Id, document.StoreId, document.FileName, text, ct);

        auditWriter.Record("document.uploaded", nameof(Document), document.Id.ToString(), new { document.FileName });

        return document;
    }

    public async Task<PagedResult<Document>> ListAsync(PageRequest page, CancellationToken ct)
    {
        var (items, totalCount) = await documents.ListAsync(page.Skip, page.PageSize, ct);
        return new PagedResult<Document> { Items = items, Page = page.Page, PageSize = page.PageSize, TotalCount = totalCount };
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var document = await documents.GetByIdAsync(id, ct) ?? throw new NotFoundException(nameof(Document), id);

        await ingestion.DeleteChunksAsync(document.Id, ct);
        documents.Remove(document);
        await unitOfWork.SaveChangesAsync(ct);
        await storage.DeleteAsync(document.StorageKey, ct);

        auditWriter.Record("document.deleted", nameof(Document), document.Id.ToString(), new { document.FileName });
    }
}
