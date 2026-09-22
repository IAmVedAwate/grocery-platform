using System.Net;
using System.Net.Http.Json;
using Domain.Identity;
using Integration.Helpers;
using Xunit;
using static Integration.Helpers.ApiTestHelper;

namespace Integration;

/// <summary>
/// Covers the "not RBAC, a real per-user permission checklist" model
/// (UserPermissionEntity) — a staff account's access is whatever set of
/// permission keys is directly assigned to them, editable at any time,
/// independent of any role.
/// </summary>
[Collection("Integration")]
public class StaffPermissionTests(CustomWebApplicationFactory factory)
{
    private const string StaffPassword = "P@ssword123!";

    [Fact]
    public async Task StaffUser_CanOnlyDoWhatTheirExplicitPermissionsAllow()
    {
        var client = factory.CreateClient();
        var owner = await AuthTestHelper.RegisterAndLoginAsync(client, "staff-scope");

        var staffEmail = $"cashier-{Guid.NewGuid():N}@test.local";
        var create = await Authorized(client, HttpMethod.Post, "/api/v1/users", owner.AccessToken,
            body: new { email = staffEmail, password = StaffPassword, displayName = "Cashier One", permissions = new[] { Permissions.CatalogRead } });
        create.EnsureSuccessStatusCode();

        var staffToken = await LoginAsync(client, owner.Slug, staffEmail, StaffPassword);

        // Granted: can read the catalog.
        var read = await Authorized(client, HttpMethod.Get, "/api/v1/products", staffToken);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);

        // Not granted: cannot create a product.
        var write = await Authorized(client, HttpMethod.Post, "/api/v1/products", staffToken,
            body: new { sku = "X", name = "X", price = 1m, taxRatePercent = 0m, barcode = (string?)null, categoryId = (Guid?)null, brandId = (Guid?)null, unitId = (Guid?)null, lowStockThreshold = 0 });
        Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);
    }

    [Fact]
    public async Task UpdatePermissions_TakesEffectOnNextLogin()
    {
        var client = factory.CreateClient();
        var owner = await AuthTestHelper.RegisterAndLoginAsync(client, "staff-update");

        var staffEmail = $"trainee-{Guid.NewGuid():N}@test.local";
        var create = await Authorized(client, HttpMethod.Post, "/api/v1/users", owner.AccessToken,
            body: new { email = staffEmail, password = StaffPassword, displayName = "Trainee", permissions = Array.Empty<string>() });
        create.EnsureSuccessStatusCode();
        var userId = Guid.Parse((await create.Content.ReadAsStringAsync()).Trim('"'));

        var firstToken = await LoginAsync(client, owner.Slug, staffEmail, StaffPassword);
        var beforeGrant = await Authorized(client, HttpMethod.Get, "/api/v1/products", firstToken);
        Assert.Equal(HttpStatusCode.Forbidden, beforeGrant.StatusCode);

        var grant = await Authorized(client, HttpMethod.Put, $"/api/v1/users/{userId}/permissions", owner.AccessToken,
            body: new { permissions = new[] { Permissions.CatalogRead } });
        Assert.Equal(HttpStatusCode.NoContent, grant.StatusCode);

        var secondToken = await LoginAsync(client, owner.Slug, staffEmail, StaffPassword);
        var afterGrant = await Authorized(client, HttpMethod.Get, "/api/v1/products", secondToken);
        Assert.Equal(HttpStatusCode.OK, afterGrant.StatusCode);
    }

    [Fact]
    public async Task CreateStaff_WithUnknownPermissionKey_ReturnsValidationError()
    {
        var client = factory.CreateClient();
        var owner = await AuthTestHelper.RegisterAndLoginAsync(client, "staff-badkey");

        var response = await Authorized(client, HttpMethod.Post, "/api/v1/users", owner.AccessToken,
            body: new { email = $"x-{Guid.NewGuid():N}@test.local", password = StaffPassword, displayName = "X", permissions = new[] { "not.a.real.permission" } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Users_AreTenantScoped()
    {
        var client = factory.CreateClient();
        var storeA = await AuthTestHelper.RegisterAndLoginAsync(client, "staff-tenant-a");
        var storeB = await AuthTestHelper.RegisterAndLoginAsync(client, "staff-tenant-b");

        var create = await Authorized(client, HttpMethod.Post, "/api/v1/users", storeA.AccessToken,
            body: new { email = $"a-{Guid.NewGuid():N}@test.local", password = StaffPassword, displayName = "A Staff", permissions = new[] { Permissions.CatalogRead } });
        create.EnsureSuccessStatusCode();
        var userIdA = Guid.Parse((await create.Content.ReadAsStringAsync()).Trim('"'));

        var crossTenantEdit = await Authorized(client, HttpMethod.Put, $"/api/v1/users/{userIdA}/permissions", storeB.AccessToken,
            body: new { permissions = new[] { Permissions.CatalogManage } });
        Assert.Equal(HttpStatusCode.NotFound, crossTenantEdit.StatusCode);

        var storeBList = await Authorized(client, HttpMethod.Get, "/api/v1/users", storeB.AccessToken);
        storeBList.EnsureSuccessStatusCode();
        var listed = await storeBList.Content.ReadFromJsonAsync<List<StaffUserDtoLike>>();
        Assert.DoesNotContain(listed!, u => u.Id == userIdA);
    }

    [Fact]
    public async Task CannotDeactivateOwnAccount()
    {
        var client = factory.CreateClient();
        var owner = await AuthTestHelper.RegisterAndLoginAsync(client, "staff-selfdeactivate");

        var list = await Authorized(client, HttpMethod.Get, "/api/v1/users", owner.AccessToken);
        list.EnsureSuccessStatusCode();
        var users = await list.Content.ReadFromJsonAsync<List<StaffUserDtoLike>>();
        var self = Assert.Single(users!);

        var deactivate = await Authorized(client, HttpMethod.Post, $"/api/v1/users/{self.Id}/deactivate", owner.AccessToken);
        Assert.Equal(HttpStatusCode.BadRequest, deactivate.StatusCode);
    }

    [Fact]
    public async Task ListPermissions_ReturnsFullCatalog()
    {
        var client = factory.CreateClient();
        var owner = await AuthTestHelper.RegisterAndLoginAsync(client, "staff-permcatalog");

        var response = await Authorized(client, HttpMethod.Get, "/api/v1/permissions", owner.AccessToken);
        response.EnsureSuccessStatusCode();
        var keys = await response.Content.ReadFromJsonAsync<List<string>>();

        Assert.Equal(Permissions.All.Count, keys!.Count);
        Assert.Contains(Permissions.UsersManage, keys);
    }

    private static async Task<string> LoginAsync(HttpClient client, string storeSlug, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { storeSlug, email, password });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<AccessTokenResponseLike>();
        return body!.AccessToken;
    }

    private sealed record AccessTokenResponseLike(string AccessToken);
    private sealed record StaffUserDtoLike(Guid Id, string Email, string DisplayName, bool IsActive, List<string> Permissions);
}
