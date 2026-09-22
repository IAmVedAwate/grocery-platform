using System.Net.Http.Json;
using Api.Controllers;
using Integration.Helpers;
using Xunit;
using static Integration.Helpers.ApiTestHelper;

namespace Integration;

[Collection("Integration")]
public class CustomerPurchaseHistoryTests(CustomWebApplicationFactory factory)
{
    [Fact]
    public async Task SalesOrders_FilteredByCustomerId_OnlyReturnsThatCustomersSales()
    {
        var client = factory.CreateClient();
        var store = await AuthTestHelper.RegisterAndLoginAsync(client, "purchase-history");
        var product = await CreateProductAsync(client, store.AccessToken, "HIST-1");
        await AdjustStockAsync(client, store.AccessToken, product.Id, 20);

        var customerA = await CreateCustomer(client, store.AccessToken, "Alice");
        var customerB = await CreateCustomer(client, store.AccessToken, "Bob");

        await Checkout(client, store.AccessToken, product.Id, customerA);
        await Checkout(client, store.AccessToken, product.Id, customerA);
        await Checkout(client, store.AccessToken, product.Id, customerB);
        await Checkout(client, store.AccessToken, product.Id, null); // walk-in, no customer

        var response = await Authorized(client, HttpMethod.Get, $"/api/v1/sales-orders?customerId={customerA}", store.AccessToken);
        response.EnsureSuccessStatusCode();
        var page = await response.Content.ReadFromJsonAsync<PagedResultDto<SalesOrderDto>>();

        Assert.Equal(2, page!.TotalCount);
        Assert.All(page.Items, o => Assert.Equal(customerA, o.CustomerId));
    }

    private static async Task<Guid> CreateCustomer(HttpClient client, string accessToken, string name)
    {
        var response = await Authorized(client, HttpMethod.Post, "/api/v1/customers", accessToken,
            body: new { name, phone = (string?)null, email = (string?)null });
        response.EnsureSuccessStatusCode();
        var customer = await response.Content.ReadFromJsonAsync<CustomerDto>();
        return customer!.Id;
    }

    private static async Task Checkout(HttpClient client, string accessToken, Guid productId, Guid? customerId)
    {
        var response = await Authorized(client, HttpMethod.Post, "/api/v1/sales-orders", accessToken,
            body: new { customerId, paymentMethod = "Cash", lines = new[] { new { productId, quantity = 1, lineDiscount = (decimal?)null } } },
            idempotencyKey: Guid.NewGuid().ToString());
        response.EnsureSuccessStatusCode();
    }
}
