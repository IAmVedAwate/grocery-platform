using System.Net.Http.Json;
using Application.Notifications;
using Integration.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static Integration.Helpers.ApiTestHelper;

namespace Integration;

/// <summary>
/// Exercises ILowStockNotificationGenerator directly — the same entry
/// point LowStockNotificationWorker's timer loop calls — rather than
/// waiting on the real background timer, matching how RateLimitingTests
/// avoids depending on wall-clock timing wherever the underlying logic can
/// be invoked directly instead.
/// </summary>
[Collection("Integration")]
public class LowStockNotificationTests(CustomWebApplicationFactory factory)
{
    [Fact]
    public async Task GenerateAsync_CreatesNotification_ForProductBelowThreshold_AndDedupesUntilAcknowledged()
    {
        var client = factory.CreateClient();
        var store = await AuthTestHelper.RegisterAndLoginAsync(client, "lowstock");
        var product = await CreateProductAsync(client, store.AccessToken, "LS-1");
        // LowStockThreshold defaults to 0 on CreateProductAsync's helper —
        // raise it via update so 2 units on hand is genuinely below it.
        var update = await Authorized(client, HttpMethod.Put, $"/api/v1/products/{product.Id}", store.AccessToken,
            body: new { name = product.Name, price = product.Price, taxRatePercent = product.TaxRatePercent, barcode = (string?)null, categoryId = (Guid?)null, brandId = (Guid?)null, unitId = (Guid?)null, lowStockThreshold = 5 });
        update.EnsureSuccessStatusCode();
        await AdjustStockAsync(client, store.AccessToken, product.Id, 2);

        // GenerateAsync is a genuine cross-tenant sweep (by design — see
        // LowStockNotificationGenerator's remarks), and the integration
        // suite shares one SQL Server container across every test class,
        // so this sweep also picks up any other still-open low-stock
        // product left behind by every OTHER test that ran earlier in the
        // same run. Assert "at least our one new notification" here, not
        // an exact global count; the per-store/per-product assertions
        // below (and the delta assertions after) are what actually verify
        // correctness for this test's own data.
        var created = await GenerateAsync();
        Assert.True(created >= 1, $"Expected at least 1 new notification, got {created}.");

        var list = await GetNotifications(client, store.AccessToken);
        var notification = Assert.Single(list, n => n.Payload.Contains(product.Id.ToString()));
        Assert.Equal("LowStock", notification.Type);
        Assert.False(notification.IsRead);

        // Still below threshold, but an unread notification already exists — no duplicate.
        var secondSweep = await GenerateAsync();
        Assert.Equal(0, secondSweep);

        var markRead = await Authorized(client, HttpMethod.Post, $"/api/v1/notifications/{notification.Id}/read", store.AccessToken);
        Assert.Equal(System.Net.HttpStatusCode.NoContent, markRead.StatusCode);

        // Still below threshold, but now the prior notification is resolved — a fresh one is raised.
        var thirdSweep = await GenerateAsync();
        Assert.Equal(1, thirdSweep);
    }

    [Fact]
    public async Task Notifications_AreTenantScoped()
    {
        var client = factory.CreateClient();
        var storeA = await AuthTestHelper.RegisterAndLoginAsync(client, "lowstock-tenant-a");
        var storeB = await AuthTestHelper.RegisterAndLoginAsync(client, "lowstock-tenant-b");
        var product = await CreateProductAsync(client, storeA.AccessToken, "LS-2");
        var update = await Authorized(client, HttpMethod.Put, $"/api/v1/products/{product.Id}", storeA.AccessToken,
            body: new { name = product.Name, price = product.Price, taxRatePercent = product.TaxRatePercent, barcode = (string?)null, categoryId = (Guid?)null, brandId = (Guid?)null, unitId = (Guid?)null, lowStockThreshold = 5 });
        update.EnsureSuccessStatusCode();

        await GenerateAsync();

        var storeBList = await GetNotifications(client, storeB.AccessToken);
        Assert.DoesNotContain(storeBList, n => n.Payload.Contains(product.Id.ToString()));

        var storeAList = await GetNotifications(client, storeA.AccessToken);
        Assert.Contains(storeAList, n => n.Payload.Contains(product.Id.ToString()));
    }

    private async Task<int> GenerateAsync()
    {
        using var scope = factory.Services.CreateScope();
        var generator = scope.ServiceProvider.GetRequiredService<ILowStockNotificationGenerator>();
        return await generator.GenerateAsync(CancellationToken.None);
    }

    private static async Task<List<NotificationDtoLike>> GetNotifications(HttpClient client, string accessToken)
    {
        var response = await Authorized(client, HttpMethod.Get, "/api/v1/notifications", accessToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PagedResultDto<NotificationDtoLike>>();
        return result!.Items.ToList();
    }

    private sealed record NotificationDtoLike(Guid Id, string Type, Guid? ReferenceId, string Payload, bool IsRead, DateTime CreatedAtUtc);
}
