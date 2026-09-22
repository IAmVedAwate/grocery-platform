using System.Text.Json;
using Application.Catalog;
using Application.Common;
using Application.Inventory;
using Application.Purchasing;
using Application.Reporting;
using Application.Sales;
using Domain.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.VectorData;
using Shared.Exceptions;

namespace Infrastructure.Ai;

/// <summary>
/// The tool-calling registry's actual logic (docs/PRD.md §15/§27), kept
/// separate from GeminiAiAssistantService's agent-orchestration code
/// specifically so it's directly unit-testable — mock the Application
/// services + IHttpContextAccessor below and assert the permission gate
/// and delegation, no LLM or agent framework involved at all. That
/// framework is exactly the part that should NOT be faked in an automated
/// test (docs/checkpoints/skills-inventory.md — "don't spend tokens on
/// every test run"): convincingly faking "did the model pick the right
/// tool" isn't meaningful to fake, so that's left to one deliberate,
/// manual, minimal-token smoke test instead.
///
/// Every method checks the SAME permission its equivalent REST endpoint
/// requires, against the real authenticated ClaimsPrincipal — a tool
/// being callable is not itself authorization.
/// </summary>
public sealed class AiTools(
    ProductApplicationService products,
    InventoryApplicationService inventory,
    ReportingApplicationService reporting,
    PurchasingApplicationService purchasing,
    SupplierApplicationService suppliers,
    CustomerApplicationService customers,
    VectorStore vectorStore,
    ITenantContext tenantContext,
    IHttpContextAccessor httpContextAccessor)
{
    private const string DocumentChunkCollectionName = "document_chunks";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public void EnsurePermission(string permission)
    {
        var user = httpContextAccessor.HttpContext?.User;
        if (user is null || !user.HasClaim("permission", permission))
            throw new ForbiddenAppException($"This action requires the '{permission}' permission.");
    }

    private static string Json(object value) => JsonSerializer.Serialize(value, JsonOptions);

    public async Task<string> SearchProductsAsync(string query, CancellationToken ct)
    {
        EnsurePermission(Permissions.CatalogRead);
        var result = await products.ListAsync(new PageRequest(1, 10), query, isActive: true, ct);
        return Json(result.Items.Select(p => new { p.Sku, p.Name, p.Price, p.TaxRatePercent, p.LowStockThreshold }));
    }

    public async Task<string> GetInventoryAsync(string? search, CancellationToken ct)
    {
        EnsurePermission(Permissions.InventoryRead);
        var (items, _) = await inventory.ListOverviewAsync(new PageRequest(1, 20), search, false, ct);
        return Json(items.Select(i => new { i.Sku, i.Name, i.QuantityOnHand, i.LowStockThreshold, i.IsLowStock }));
    }

    public async Task<string> GetLowStockItemsAsync(CancellationToken ct)
    {
        EnsurePermission(Permissions.InventoryRead);
        var (items, _) = await inventory.ListOverviewAsync(new PageRequest(1, 50), null, true, ct);
        return Json(items.Select(i => new { i.Sku, i.Name, i.QuantityOnHand, i.LowStockThreshold }));
    }

    public async Task<string> GetSalesSummaryAsync(DateTime fromDate, DateTime toDate, CancellationToken ct)
    {
        EnsurePermission(Permissions.ReportsView);
        var result = await reporting.GetSalesByDayAsync(fromDate, toDate, new PageRequest(1, 31), ct);
        return Json(new
        {
            days = result.Items,
            totalRevenue = result.Items.Sum(d => d.Revenue),
            totalOrders = result.Items.Sum(d => d.OrderCount)
        });
    }

    public async Task<string> GetPurchaseOrderStatusAsync(string? status, CancellationToken ct)
    {
        EnsurePermission(Permissions.CatalogRead);
        Domain.Purchasing.PurchaseOrderStatus? parsedStatus =
            status is not null && Enum.TryParse<Domain.Purchasing.PurchaseOrderStatus>(status, true, out var s) ? s : null;
        var result = await purchasing.ListAsync(new PageRequest(1, 20), parsedStatus, ct);
        return Json(result.Items.Select(o => new { o.Id, Status = o.Status.ToString(), o.SupplierId, ItemCount = o.Items.Count }));
    }

    public async Task<string> GetSupplierStatusAsync(CancellationToken ct)
    {
        EnsurePermission(Permissions.CatalogRead);
        var result = await suppliers.ListAsync(new PageRequest(1, 20), activeOnly: null, ct);
        return Json(result.Items.Select(s => new { s.Name, Status = s.Status.ToString(), s.PaymentTermsDays }));
    }

    public async Task<string> GetCustomerSummaryAsync(string? search, CancellationToken ct)
    {
        EnsurePermission(Permissions.SalesCreate);
        var result = await customers.ListAsync(new PageRequest(1, 20), search, ct);
        return Json(result.Items.Select(c => new { c.Name, c.Phone, c.Email }));
    }

    public async Task<string> SearchDocumentsAsync(string query, List<Application.Ai.DocumentCitation> citations, CancellationToken ct)
    {
        EnsurePermission(Permissions.AiAssistantUse);

        var collection = vectorStore.GetCollection<string, DocumentChunk>(DocumentChunkCollectionName);
        if (!await collection.CollectionExistsAsync(ct))
            return "No documents have been uploaded yet.";

        var storeId = tenantContext.StoreId;
        var found = new List<string>();
        await foreach (var result in collection.SearchAsync(query, top: 5,
            new VectorSearchOptions<DocumentChunk> { Filter = c => c.StoreId == storeId }, ct))
        {
            var chunk = result.Record;
            citations.Add(new Application.Ai.DocumentCitation(chunk.DocumentId, chunk.FileName, Snippet(chunk.Content)));
            found.Add($"[Source: {chunk.FileName}]\n{chunk.Content}");
        }

        return found.Count > 0 ? string.Join("\n\n", found) : "No matching content found in uploaded documents.";
    }

    private static string Snippet(string content) => content.Length <= 200 ? content : content[..200] + "…";
}
