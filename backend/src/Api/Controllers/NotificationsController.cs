using Api.Authorization;
using Application.Common;
using Application.Notifications;
using Domain.Identity;
using Domain.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/notifications")]
public sealed class NotificationsController(NotificationApplicationService notifications) : ControllerBase
{
    [HttpGet]
    [RequirePermission(Permissions.NotificationsView)]
    public async Task<ActionResult<PagedResult<NotificationDto>>> List(
        [FromQuery] bool? unreadOnly, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await notifications.ListAsync(unreadOnly, new PageRequest(page, pageSize), ct);
        return Ok(new PagedResult<NotificationDto>
        {
            Items = result.Items.Select(NotificationDto.From).ToList(),
            Page = result.Page,
            PageSize = result.PageSize,
            TotalCount = result.TotalCount
        });
    }

    [HttpPost("{id:guid}/read")]
    [RequirePermission(Permissions.NotificationsView)]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        await notifications.MarkReadAsync(id, ct);
        return NoContent();
    }
}

public sealed record NotificationDto(Guid Id, string Type, Guid? ReferenceId, string Payload, bool IsRead, DateTime CreatedAtUtc)
{
    public static NotificationDto From(Notification n) => new(n.Id, n.Type, n.ReferenceId, n.Payload, n.IsRead, n.CreatedAtUtc);
}
