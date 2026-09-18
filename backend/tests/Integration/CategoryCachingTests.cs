using System.Net.Http.Json;
using Integration.Helpers;
using Xunit;
using static Integration.Helpers.ApiTestHelper;

namespace Integration;

[Collection("Integration")]
public class CategoryCachingTests(CustomWebApplicationFactory factory)
{
    [Fact]
    public async Task CreatedCategory_AppearsInListImmediately_CacheInvalidatedOnWrite()
    {
        var client = factory.CreateClient();
        var store = await AuthTestHelper.RegisterAndLoginAsync(client, "cat-invalidate");

        var listBefore = await Authorized(client, HttpMethod.Get, "/api/v1/categories", store.AccessToken);
        listBefore.EnsureSuccessStatusCode();
        var before = await listBefore.Content.ReadFromJsonAsync<List<CategoryDtoLike>>();
        Assert.Empty(before!);

        var create = await Authorized(client, HttpMethod.Post, "/api/v1/categories", store.AccessToken,
            body: new { name = "Dairy" });
        create.EnsureSuccessStatusCode();

        // If the cache were populated by the first (empty) list call and not
        // invalidated on write, this would still return empty for up to the
        // full 5-minute TTL.
        var listAfter = await Authorized(client, HttpMethod.Get, "/api/v1/categories", store.AccessToken);
        listAfter.EnsureSuccessStatusCode();
        var after = await listAfter.Content.ReadFromJsonAsync<List<CategoryDtoLike>>();

        var category = Assert.Single(after!);
        Assert.Equal("Dairy", category.Name);
    }

    [Fact]
    public async Task CategoryCache_DoesNotLeakAcrossTenants()
    {
        var client = factory.CreateClient();
        var storeA = await AuthTestHelper.RegisterAndLoginAsync(client, "cat-tenant-a");
        var storeB = await AuthTestHelper.RegisterAndLoginAsync(client, "cat-tenant-b");

        var createA = await Authorized(client, HttpMethod.Post, "/api/v1/categories", storeA.AccessToken,
            body: new { name = "Beverages" });
        createA.EnsureSuccessStatusCode();

        // Populate store B's cache entry as empty first...
        var listB1 = await Authorized(client, HttpMethod.Get, "/api/v1/categories", storeB.AccessToken);
        listB1.EnsureSuccessStatusCode();
        Assert.Empty((await listB1.Content.ReadFromJsonAsync<List<CategoryDtoLike>>())!);

        // ...then confirm store A's own list (a distinct cache key) still shows its category.
        var listA = await Authorized(client, HttpMethod.Get, "/api/v1/categories", storeA.AccessToken);
        listA.EnsureSuccessStatusCode();
        var itemsA = await listA.Content.ReadFromJsonAsync<List<CategoryDtoLike>>();
        Assert.Single(itemsA!, c => c.Name == "Beverages");

        // ...and store B never sees it, even after A's write.
        var listB2 = await Authorized(client, HttpMethod.Get, "/api/v1/categories", storeB.AccessToken);
        listB2.EnsureSuccessStatusCode();
        Assert.Empty((await listB2.Content.ReadFromJsonAsync<List<CategoryDtoLike>>())!);
    }

    private sealed record CategoryDtoLike(Guid Id, string Name);
}
