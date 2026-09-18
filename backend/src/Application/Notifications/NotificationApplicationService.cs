using Application.Common;
using Domain.Notifications;
using Shared.Exceptions;

namespace Application.Notifications;

public sealed class NotificationApplicationService(INotificationRepository notifications, IUnitOfWork unitOfWork)
{
    public async Task<PagedResult<Notification>> ListAsync(bool? unreadOnly, PageRequest page, CancellationToken ct)
    {
        var (items, totalCount) = await notifications.ListAsync(unreadOnly, page.Skip, page.PageSize, ct);
        return new PagedResult<Notification> { Items = items, Page = page.Page, PageSize = page.PageSize, TotalCount = totalCount };
    }

    public async Task MarkReadAsync(Guid id, CancellationToken ct)
    {
        var notification = await notifications.GetByIdAsync(id, ct) ?? throw new NotFoundException(nameof(Notification), id);
        notification.MarkRead();
        await unitOfWork.SaveChangesAsync(ct);
    }
}
