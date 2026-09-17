using System.Security.Claims;
using Api.Authorization;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace Unit.Authorization;

public class PermissionAuthorizationHandlerTests
{
    [Fact]
    public async Task Handler_WithMatchingPermissionClaim_Succeeds()
    {
        var handler = new PermissionAuthorizationHandler();
        var requirement = new PermissionRequirement("inventory.adjust");
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("permission", "inventory.adjust"), new Claim("permission", "catalog.read")],
            "TestAuth"));
        var context = new AuthorizationHandlerContext([requirement], user, resource: null);

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task Handler_WithoutMatchingPermissionClaim_DoesNotSucceed()
    {
        var handler = new PermissionAuthorizationHandler();
        var requirement = new PermissionRequirement("purchase.approve");
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("permission", "catalog.read")],
            "TestAuth"));
        var context = new AuthorizationHandlerContext([requirement], user, resource: null);

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task Handler_WithNoPermissionClaimsAtAll_DoesNotSucceed()
    {
        var handler = new PermissionAuthorizationHandler();
        var requirement = new PermissionRequirement("sales.refund");
        var user = new ClaimsPrincipal(new ClaimsIdentity("TestAuth"));
        var context = new AuthorizationHandlerContext([requirement], user, resource: null);

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }
}
