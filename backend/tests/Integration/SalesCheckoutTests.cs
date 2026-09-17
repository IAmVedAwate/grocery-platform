using System.Net;
using System.Net.Http.Json;
using Api.Controllers;
using Integration.Helpers;
using Xunit;
using static Integration.Helpers.ApiTestHelper;

namespace Integration;

[Collection("Integration")]
public class SalesCheckoutTests(CustomWebApplicationFactory factory)
{
    [Fact]
    public async Task Checkout_WithSufficientStock_DecrementsInventory_AndIssuesInvoice()
    {
        var client = factory.CreateClient();
        var store = await AuthTestHelper.RegisterAndLoginAsync(client, "checkout-happy");
        var product = await CreateProductAsync(client, store.AccessToken, "SALE-1", price: 100m, taxRatePercent: 5m);
        await AdjustStockAsync(client, store.AccessToken, product.Id, 10);

        var response = await Authorized(client, HttpMethod.Post, "/api/v1/sales-orders", store.AccessToken,
            body: new { customerId = (Guid?)null, paymentMethod = "Cash", lines = new[] { new { productId = product.Id, quantity = 3, lineDiscount = (decimal?)null } } },
            idempotencyKey: Guid.NewGuid().ToString());

        response.EnsureSuccessStatusCode();
        var order = (await response.Content.ReadFromJsonAsync<SalesOrderDto>())!;

        Assert.Equal(300m, order.SubtotalAmount);
        Assert.Equal(15m, order.TaxAmount);
        Assert.Equal(315m, order.TotalAmount);
        Assert.NotNull(order.InvoiceNumber);

        var overview = await Authorized(client, HttpMethod.Get, $"/api/v1/inventory?search=SALE-1", store.AccessToken);
        var body = await overview.Content.ReadAsStringAsync();
        Assert.Contains("\"quantityOnHand\":7", body);
    }

    [Fact]
    public async Task Checkout_WithoutIdempotencyKey_ReturnsBadRequest()
    {
        var client = factory.CreateClient();
        var store = await AuthTestHelper.RegisterAndLoginAsync(client, "checkout-no-key");
        var product = await CreateProductAsync(client, store.AccessToken, "SALE-2");
        await AdjustStockAsync(client, store.AccessToken, product.Id, 5);

        var response = await Authorized(client, HttpMethod.Post, "/api/v1/sales-orders", store.AccessToken,
            body: new { customerId = (Guid?)null, paymentMethod = "Cash", lines = new[] { new { productId = product.Id, quantity = 1, lineDiscount = (decimal?)null } } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Checkout_RetriedWithSameIdempotencyKey_ReturnsOriginalSale_DoesNotDoubleDecrementStock()
    {
        var client = factory.CreateClient();
        var store = await AuthTestHelper.RegisterAndLoginAsync(client, "checkout-idempotent");
        var product = await CreateProductAsync(client, store.AccessToken, "SALE-3");
        await AdjustStockAsync(client, store.AccessToken, product.Id, 10);
        var key = Guid.NewGuid().ToString();
        var body = new { customerId = (Guid?)null, paymentMethod = "Cash", lines = new[] { new { productId = product.Id, quantity = 4, lineDiscount = (decimal?)null } } };

        var first = await Authorized(client, HttpMethod.Post, "/api/v1/sales-orders", store.AccessToken, body, key);
        first.EnsureSuccessStatusCode();
        var firstOrder = (await first.Content.ReadFromJsonAsync<SalesOrderDto>())!;

        var second = await Authorized(client, HttpMethod.Post, "/api/v1/sales-orders", store.AccessToken, body, key);
        second.EnsureSuccessStatusCode();
        var secondOrder = (await second.Content.ReadFromJsonAsync<SalesOrderDto>())!;

        Assert.Equal(firstOrder.Id, secondOrder.Id);

        var overview = await Authorized(client, HttpMethod.Get, "/api/v1/inventory?search=SALE-3", store.AccessToken);
        var overviewBody = await overview.Content.ReadAsStringAsync();
        // 10 - 4 = 6, NOT 10 - 8 = 2 — the retry must not have decremented twice.
        Assert.Contains("\"quantityOnHand\":6", overviewBody);
    }

    [Fact]
    public async Task Checkout_WithInsufficientStock_ReturnsConflict_AndDoesNotChangeStock()
    {
        var client = factory.CreateClient();
        var store = await AuthTestHelper.RegisterAndLoginAsync(client, "checkout-insufficient");
        var product = await CreateProductAsync(client, store.AccessToken, "SALE-4");
        await AdjustStockAsync(client, store.AccessToken, product.Id, 2);

        var response = await Authorized(client, HttpMethod.Post, "/api/v1/sales-orders", store.AccessToken,
            body: new { customerId = (Guid?)null, paymentMethod = "Cash", lines = new[] { new { productId = product.Id, quantity = 5, lineDiscount = (decimal?)null } } },
            idempotencyKey: Guid.NewGuid().ToString());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var overview = await Authorized(client, HttpMethod.Get, "/api/v1/inventory?search=SALE-4", store.AccessToken);
        var overviewBody = await overview.Content.ReadAsStringAsync();
        Assert.Contains("\"quantityOnHand\":2", overviewBody);
    }

    [Fact]
    public async Task Checkout_ForInactiveProduct_ReturnsConflict()
    {
        var client = factory.CreateClient();
        var store = await AuthTestHelper.RegisterAndLoginAsync(client, "checkout-inactive");
        var product = await CreateProductAsync(client, store.AccessToken, "SALE-5");
        await AdjustStockAsync(client, store.AccessToken, product.Id, 5);
        (await Authorized(client, HttpMethod.Delete, $"/api/v1/products/{product.Id}", store.AccessToken)).EnsureSuccessStatusCode();

        var response = await Authorized(client, HttpMethod.Post, "/api/v1/sales-orders", store.AccessToken,
            body: new { customerId = (Guid?)null, paymentMethod = "Cash", lines = new[] { new { productId = product.Id, quantity = 1, lineDiscount = (decimal?)null } } },
            idempotencyKey: Guid.NewGuid().ToString());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Refund_RestoresStock_AndCannotExceedOriginalQuantity()
    {
        var client = factory.CreateClient();
        var store = await AuthTestHelper.RegisterAndLoginAsync(client, "checkout-refund");
        var product = await CreateProductAsync(client, store.AccessToken, "SALE-6");
        await AdjustStockAsync(client, store.AccessToken, product.Id, 10);

        var checkout = await Authorized(client, HttpMethod.Post, "/api/v1/sales-orders", store.AccessToken,
            body: new { customerId = (Guid?)null, paymentMethod = "Cash", lines = new[] { new { productId = product.Id, quantity = 3, lineDiscount = (decimal?)null } } },
            idempotencyKey: Guid.NewGuid().ToString());
        checkout.EnsureSuccessStatusCode();
        var order = (await checkout.Content.ReadFromJsonAsync<SalesOrderDto>())!;

        // Over-refund is rejected.
        var overRefund = await Authorized(client, HttpMethod.Post, $"/api/v1/sales-orders/{order.Id}/refund", store.AccessToken,
            new { lines = new[] { new { productId = product.Id, quantity = 4 } } });
        Assert.Equal(HttpStatusCode.Conflict, overRefund.StatusCode);

        var refund = await Authorized(client, HttpMethod.Post, $"/api/v1/sales-orders/{order.Id}/refund", store.AccessToken,
            new { lines = new[] { new { productId = product.Id, quantity = 2 } } });
        refund.EnsureSuccessStatusCode();
        var refunded = (await refund.Content.ReadFromJsonAsync<SalesOrderDto>())!;
        Assert.Equal("PartiallyRefunded", refunded.Status);

        var overview = await Authorized(client, HttpMethod.Get, "/api/v1/inventory?search=SALE-6", store.AccessToken);
        var overviewBody = await overview.Content.ReadAsStringAsync();
        // 10 - 3 (sold) + 2 (refunded) = 9
        Assert.Contains("\"quantityOnHand\":9", overviewBody);
    }

    /// <summary>
    /// The highest-value test in this project (docs/PRD.md §23, §7): two
    /// cashiers try to sell the last unit of stock at the same moment.
    /// Exactly one must succeed; the other must fail cleanly with 409; the
    /// final stock must never go negative. Both requests use DIFFERENT
    /// idempotency keys — this is two genuinely independent concurrent
    /// sale attempts, not a retry of the same request (that's the
    /// idempotency test above).
    /// </summary>
    [Fact]
    public async Task Checkout_TwoSimultaneousSalesForTheLastUnit_ExactlyOneSucceeds()
    {
        var client = factory.CreateClient();
        var store = await AuthTestHelper.RegisterAndLoginAsync(client, "checkout-race");
        var product = await CreateProductAsync(client, store.AccessToken, "SALE-RACE");
        await AdjustStockAsync(client, store.AccessToken, product.Id, 1);

        object CheckoutBody() => new
        {
            customerId = (Guid?)null,
            paymentMethod = "Cash",
            lines = new[] { new { productId = product.Id, quantity = 1, lineDiscount = (decimal?)null } }
        };

        var clientA = factory.CreateClient();
        var clientB = factory.CreateClient();

        var taskA = Authorized(clientA, HttpMethod.Post, "/api/v1/sales-orders", store.AccessToken, CheckoutBody(), Guid.NewGuid().ToString());
        var taskB = Authorized(clientB, HttpMethod.Post, "/api/v1/sales-orders", store.AccessToken, CheckoutBody(), Guid.NewGuid().ToString());
        var results = await Task.WhenAll(taskA, taskB);

        var successCount = results.Count(r => r.StatusCode == HttpStatusCode.Created);
        var conflictCount = results.Count(r => r.StatusCode == HttpStatusCode.Conflict);

        Assert.Equal(1, successCount);
        Assert.Equal(1, conflictCount);

        var overview = await Authorized(client, HttpMethod.Get, "/api/v1/inventory?search=SALE-RACE", store.AccessToken);
        var overviewBody = await overview.Content.ReadAsStringAsync();
        // Exactly 0, not negative — the only other value QuantityOnHand
        // could take that this string DOESN'T match. (A separate
        // Assert.DoesNotContain("-1", ...) was removed here: it's a false
        // assertion against a JSON body full of GUIDs, which routinely
        // contain the substring "-1" by pure chance — a test bug, not a
        // product one, caught by this exact test flaking on a later run.)
        Assert.Contains("\"quantityOnHand\":0", overviewBody);
    }
}
