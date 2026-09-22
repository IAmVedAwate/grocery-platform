using Application.Ai;
using Domain.Ai;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public sealed class DocumentRepository(GroceryDbContext db) : IDocumentRepository
{
    public async Task AddAsync(Document document, CancellationToken ct) => await db.Documents.AddAsync(document, ct);

    public Task<Document?> GetByIdAsync(Guid id, CancellationToken ct) =>
        db.Documents.FirstOrDefaultAsync(d => d.Id == id, ct);

    public async Task<(IReadOnlyList<Document> Items, int TotalCount)> ListAsync(int skip, int take, CancellationToken ct)
    {
        var query = db.Documents.AsNoTracking();
        var totalCount = await query.CountAsync(ct);
        var items = await query.OrderByDescending(d => d.UploadedAtUtc).Skip(skip).Take(take).ToListAsync(ct);
        return (items, totalCount);
    }

    public void Remove(Document document) => db.Documents.Remove(document);
}
