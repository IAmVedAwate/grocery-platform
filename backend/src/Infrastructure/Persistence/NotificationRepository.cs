using Application.Notifications;
using Domain.Notifications;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public sealed class NotificationRepository(GroceryDbContext db) : INotificationRepository
{
    public async Task<(IReadOnlyList<Notification> Items, int TotalCount)> ListAsync(
        bool? unreadOnly, int skip, int take, CancellationToken ct)
    {
        var query = db.Notifications.AsNoTracking().AsQueryable();

        if (unreadOnly == true)
            query = query.Where(n => !n.IsRead);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(n => n.CreatedAtUtc)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public Task<Notification?> GetByIdAsync(Guid id, CancellationToken ct) =>
        db.Notifications.FirstOrDefaultAsync(n => n.Id == id, ct);
}
