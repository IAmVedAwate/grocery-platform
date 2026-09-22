using Api.Authorization;
using Application.Identity;
using Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>
/// Staff account + permission management. Every action requires
/// users.manage — there is no separate "view only" tier here, since
/// seeing another staff member's exact permission set is itself
/// sensitive (docs/PRD.md §5.1).
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/users")]
public sealed class UsersController(UserManagementApplicationService users) : ControllerBase
{
    [HttpGet]
    [RequirePermission(Permissions.UsersManage)]
    public async Task<ActionResult<IReadOnlyList<StaffUserDto>>> List(CancellationToken ct)
    {
        var staff = await users.ListStaffAsync(ct);
        return Ok(staff.Select(StaffUserDto.From).ToList());
    }

    [HttpPost]
    [RequirePermission(Permissions.UsersManage)]
    public async Task<ActionResult<Guid>> Create(CreateStaffDto dto, CancellationToken ct)
    {
        var userId = await users.CreateStaffAsync(
            new CreateStaffRequest(dto.Email, dto.Password, dto.DisplayName, dto.Permissions), ct);
        return Created($"/api/v1/users/{userId}", userId);
    }

    [HttpPut("{id:guid}/permissions")]
    [RequirePermission(Permissions.UsersManage)]
    public async Task<IActionResult> UpdatePermissions(Guid id, UpdatePermissionsDto dto, CancellationToken ct)
    {
        await users.UpdatePermissionsAsync(id, dto.Permissions, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/deactivate")]
    [RequirePermission(Permissions.UsersManage)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        await users.SetActiveAsync(id, false, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/activate")]
    [RequirePermission(Permissions.UsersManage)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
    {
        await users.SetActiveAsync(id, true, ct);
        return NoContent();
    }

    /// <summary>The full permission catalog, so a Settings screen can
    /// render the checklist without hardcoding permission keys client-side.</summary>
    [HttpGet("~/api/v1/permissions")]
    [RequirePermission(Permissions.UsersManage)]
    public ActionResult<IReadOnlyList<string>> ListPermissions() => Ok(Permissions.All);
}

public sealed record CreateStaffDto(string Email, string Password, string DisplayName, IReadOnlyList<string> Permissions);
public sealed record UpdatePermissionsDto(IReadOnlyList<string> Permissions);

public sealed record StaffUserDto(Guid Id, string Email, string DisplayName, bool IsActive, IReadOnlyList<string> Permissions)
{
    public static StaffUserDto From(StaffUserSnapshot s) => new(s.UserId, s.Email, s.DisplayName, s.IsActive, s.Permissions);
}
