using Domain.Ai;

namespace Application.Ai;

public interface IDocumentRepository
{
    Task AddAsync(Document document, CancellationToken ct);
    Task<Document?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<(IReadOnlyList<Document> Items, int TotalCount)> ListAsync(int skip, int take, CancellationToken ct);
    void Remove(Document document);
}
