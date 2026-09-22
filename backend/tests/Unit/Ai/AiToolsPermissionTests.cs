using System.Security.Claims;
using Domain.Identity;
using Infrastructure.Ai;
using Microsoft.AspNetCore.Http;
using Shared.Exceptions;
using Xunit;

namespace Unit.Ai;

/// <summary>
/// Every tool must check its OWN permission before touching any
/// collaborator (docs/PRD.md §15/§27) — a Cashier asking the assistant to
/// approve a purchase order must be refused exactly like they'd be refused
/// calling the REST endpoint directly. Proven here by constructing AiTools
/// with every Application-service/VectorStore collaborator left `null!`:
/// if a tool method touched one of them before checking permission, this
/// test would fail with a NullReferenceException instead of the expected
/// ForbiddenAppException. No LLM, DB, or vector store needed — see
/// AiTools.cs for why this logic is split out specifically to make that
/// possible.
/// </summary>
public class AiToolsPermissionTests
{
    private static AiTools CreateSut(params string[] grantedPermissions)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                grantedPermissions.Select(p => new Claim("permission", p)), "TestAuth"))
        };
        var accessor = new HttpContextAccessor { HttpContext = httpContext };

        return new AiTools(
            products: null!, inventory: null!, reporting: null!, purchasing: null!,
            suppliers: null!, customers: null!, vectorStore: null!,
            tenantContext: null!, httpContextAccessor: accessor);
    }

    [Theory]
    [InlineData(Permissions.CatalogRead)]
    [InlineData(Permissions.InventoryRead)]
    [InlineData(Permissions.ReportsView)]
    [InlineData(Permissions.SalesCreate)]
    [InlineData(Permissions.AiAssistantUse)]
    public void EnsurePermission_WithMatchingClaim_DoesNotThrow(string permission)
    {
        var sut = CreateSut(permission);

        sut.EnsurePermission(permission);
    }

    [Fact]
    public async Task SearchProductsAsync_WithoutCatalogRead_ThrowsForbidden_BeforeTouchingProductService()
    {
        var sut = CreateSut(); // no permissions granted

        await Assert.ThrowsAsync<ForbiddenAppException>(() => sut.SearchProductsAsync("rice", CancellationToken.None));
    }

    [Fact]
    public async Task GetInventoryAsync_WithoutInventoryRead_ThrowsForbidden()
    {
        var sut = CreateSut(Permissions.CatalogRead); // unrelated permission granted

        await Assert.ThrowsAsync<ForbiddenAppException>(() => sut.GetInventoryAsync(null, CancellationToken.None));
    }

    [Fact]
    public async Task GetLowStockItemsAsync_WithoutInventoryRead_ThrowsForbidden()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<ForbiddenAppException>(() => sut.GetLowStockItemsAsync(CancellationToken.None));
    }

    [Fact]
    public async Task GetSalesSummaryAsync_WithoutReportsView_ThrowsForbidden()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<ForbiddenAppException>(() =>
            sut.GetSalesSummaryAsync(DateTime.UtcNow.AddDays(-7), DateTime.UtcNow, CancellationToken.None));
    }

    [Fact]
    public async Task GetPurchaseOrderStatusAsync_WithoutCatalogRead_ThrowsForbidden()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<ForbiddenAppException>(() => sut.GetPurchaseOrderStatusAsync(null, CancellationToken.None));
    }

    [Fact]
    public async Task GetSupplierStatusAsync_WithoutCatalogRead_ThrowsForbidden()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<ForbiddenAppException>(() => sut.GetSupplierStatusAsync(CancellationToken.None));
    }

    [Fact]
    public async Task GetCustomerSummaryAsync_WithoutSalesCreate_ThrowsForbidden()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<ForbiddenAppException>(() => sut.GetCustomerSummaryAsync(null, CancellationToken.None));
    }

    [Fact]
    public async Task SearchDocumentsAsync_WithoutAiAssistantUse_ThrowsForbidden_BeforeTouchingVectorStore()
    {
        var sut = CreateSut(); // note: no AiAssistantUse — must fail before vectorStore (null!) is ever touched

        await Assert.ThrowsAsync<ForbiddenAppException>(() =>
            sut.SearchDocumentsAsync("return policy", [], CancellationToken.None));
    }

    [Fact]
    public void EnsurePermission_WithNoAuthenticatedUserAtAll_ThrowsForbidden()
    {
        var accessor = new HttpContextAccessor(); // HttpContext is null — no request in flight

        var sut = new AiTools(
            products: null!, inventory: null!, reporting: null!, purchasing: null!,
            suppliers: null!, customers: null!, vectorStore: null!,
            tenantContext: null!, httpContextAccessor: accessor);

        Assert.Throws<ForbiddenAppException>(() => sut.EnsurePermission(Permissions.CatalogRead));
    }
}
