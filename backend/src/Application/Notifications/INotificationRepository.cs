using Domain.Notifications;

namespace Application.Notifications;

public interface INotificationRepository
{
    Task<(IReadOnlyList<Notification> Items, int TotalCount)> ListAsync(bool? unreadOnly, int skip, int take, CancellationToken ct);
    Task<Notification?> GetByIdAsync(Guid id, CancellationToken ct);
}
