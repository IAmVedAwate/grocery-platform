using System.Net;
using System.Net.Http.Json;
using Api.Controllers;
using Integration.Helpers;
using Xunit;
using static Integration.Helpers.ApiTestHelper;

namespace Integration;

[Collection("Integration")]
public class PurchasingWorkflowTests(CustomWebApplicationFactory factory)
{
    [Fact]
    public async Task FullCycle_CreateSubmitApproveReceive_IncreasesInventory()
    {
        var client = factory.CreateClient();
        var store = await AuthTestHelper.RegisterAndLoginAsync(client, "po-full-cycle");
        var product = await CreateProductAsync(client, store.AccessToken, "PO-1");
        var supplier = await CreateSupplierAsync(client, store.AccessToken);

        var create = await Authorized(client, HttpMethod.Post, "/api/v1/purchase-orders", store.AccessToken, new
        {
            supplierId = supplier.Id,
            lines = new[] { new { productId = product.Id, quantity = 50, unitCost = 10.00m } }
        });
        create.EnsureSuccessStatusCode();
        var order = (await create.Content.ReadFromJsonAsync<PurchaseOrderDto>())!;
        Assert.Equal("Draft", order.Status);

        await Authorized(client, HttpMethod.Post, $"/api/v1/purchase-orders/{order.Id}/submit", store.AccessToken);
        var approve = await Authorized(client, HttpMethod.Post, $"/api/v1/purchase-orders/{order.Id}/approve", store.AccessToken);
        approve.EnsureSuccessStatusCode();
        Assert.Equal("Approved", (await approve.Content.ReadFromJsonAsync<PurchaseOrderDto>())!.Status);

        var receive = await Authorized(client, HttpMethod.Post, $"/api/v1/purchase-orders/{order.Id}/receive", store.AccessToken, new
        {
            lines = new[] { new { productId = product.Id, quantity = 50 } }
        });
        receive.EnsureSuccessStatusCode();
        var received = (await receive.Content.ReadFromJsonAsync<PurchaseOrderDto>())!;
        Assert.Equal("Received", received.Status);

        var inventory = await Authorized(client, HttpMethod.Get, $"/api/v1/inventory/{product.Id}/history", store.AccessToken);
        var movements = await inventory.Content.ReadFromJsonAsync<List<StockMovementDto>>();
        Assert.Contains(movements!, m => m.Type == "Receipt" && m.QuantityDelta == 50 && m.ReferenceId == order.Id);
    }

    [Fact]
    public async Task PartialReceive_LeavesOrderPartiallyReceived_AndCanBeCompletedLater()
    {
        var client = factory.CreateClient();
        var store = await AuthTestHelper.RegisterAndLoginAsync(client, "po-partial");
        var product = await CreateProductAsync(client, store.AccessToken, "PO-2");
        var supplier = await CreateSupplierAsync(client, store.AccessToken);

        var order = await CreateApprovedOrderAsync(client, store.AccessToken, supplier.Id, product.Id, quantity: 20);

        var firstReceive = await Authorized(client, HttpMethod.Post, $"/api/v1/purchase-orders/{order.Id}/receive", store.AccessToken, new
        {
            lines = new[] { new { productId = product.Id, quantity = 12 } }
        });
        firstReceive.EnsureSuccessStatusCode();
        Assert.Equal("PartiallyReceived", (await firstReceive.Content.ReadFromJsonAsync<PurchaseOrderDto>())!.Status);

        var secondReceive = await Authorized(client, HttpMethod.Post, $"/api/v1/purchase-orders/{order.Id}/receive", store.AccessToken, new
        {
            lines = new[] { new { productId = product.Id, quantity = 8 } }
        });
        secondReceive.EnsureSuccessStatusCode();
        Assert.Equal("Received", (await secondReceive.Content.ReadFromJsonAsync<PurchaseOrderDto>())!.Status);
    }

    [Fact]
    public async Task Receive_WithoutApprovalFirst_ReturnsConflict()
    {
        var client = factory.CreateClient();
        var store = await AuthTestHelper.RegisterAndLoginAsync(client, "po-unapproved");
        var product = await CreateProductAsync(client, store.AccessToken, "PO-3");
        var supplier = await CreateSupplierAsync(client, store.AccessToken);

        var create = await Authorized(client, HttpMethod.Post, "/api/v1/purchase-orders", store.AccessToken, new
        {
            supplierId = supplier.Id,
            lines = new[] { new { productId = product.Id, quantity = 5, unitCost = 1.00m } }
        });
        var order = (await create.Content.ReadFromJsonAsync<PurchaseOrderDto>())!;

        // Never submitted or approved — Draft.
        var receive = await Authorized(client, HttpMethod.Post, $"/api/v1/purchase-orders/{order.Id}/receive", store.AccessToken, new
        {
            lines = new[] { new { productId = product.Id, quantity = 5 } }
        });

        // A business-rule violation (invalid state transition) is a client
        // error, not a server fault — see Application.Common.DomainRuleGuard.
        Assert.Equal(HttpStatusCode.Conflict, receive.StatusCode);
    }

    [Fact]
    public async Task Receive_MoreThanOrdered_ReturnsServerError_NotSilentOverReceipt()
    {
        var client = factory.CreateClient();
        var store = await AuthTestHelper.RegisterAndLoginAsync(client, "po-overreceive");
        var product = await CreateProductAsync(client, store.AccessToken, "PO-4");
        var supplier = await CreateSupplierAsync(client, store.AccessToken);
        var order = await CreateApprovedOrderAsync(client, store.AccessToken, supplier.Id, product.Id, quantity: 5);

        var receive = await Authorized(client, HttpMethod.Post, $"/api/v1/purchase-orders/{order.Id}/receive", store.AccessToken, new
        {
            lines = new[] { new { productId = product.Id, quantity = 6 } }
        });

        Assert.Equal(HttpStatusCode.Conflict, receive.StatusCode);

        // No stock was recorded and the order was NOT silently over-received.
        var history = await Authorized(client, HttpMethod.Get, $"/api/v1/inventory/{product.Id}/history", store.AccessToken);
        var movements = await history.Content.ReadFromJsonAsync<List<StockMovementDto>>();
        Assert.Empty(movements!);
    }

    private static async Task<SupplierDto> CreateSupplierAsync(HttpClient client, string accessToken)
    {
        var response = await Authorized(client, HttpMethod.Post, "/api/v1/suppliers", accessToken, new
        {
            name = $"Test Supplier {Guid.NewGuid():N}", contactInfo = (string?)null, paymentTermsDays = (int?)null
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SupplierDto>())!;
    }

    private static async Task<PurchaseOrderDto> CreateApprovedOrderAsync(HttpClient client, string accessToken, Guid supplierId, Guid productId, int quantity)
    {
        var create = await Authorized(client, HttpMethod.Post, "/api/v1/purchase-orders", accessToken, new
        {
            supplierId,
            lines = new[] { new { productId, quantity, unitCost = 1.00m } }
        });
        create.EnsureSuccessStatusCode();
        var order = (await create.Content.ReadFromJsonAsync<PurchaseOrderDto>())!;

        await Authorized(client, HttpMethod.Post, $"/api/v1/purchase-orders/{order.Id}/submit", accessToken);
        var approve = await Authorized(client, HttpMethod.Post, $"/api/v1/purchase-orders/{order.Id}/approve", accessToken);
        approve.EnsureSuccessStatusCode();
        return (await approve.Content.ReadFromJsonAsync<PurchaseOrderDto>())!;
    }
}
