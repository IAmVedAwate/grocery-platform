using System.ComponentModel;
using Application.Ai;
using Microsoft.Extensions.AI;

namespace Infrastructure.Ai;

/// <summary>
/// The tool-calling registry (docs/PRD.md §15) and RAG (§16) live in the
/// same agent on purpose: a real question ("what's low on stock" vs
/// "what's our return policy") needs different data sources, and the
/// model — not a hardcoded route — is what should decide which tool(s)
/// apply, exactly like the reference project's /ask-agent endpoint this
/// was adapted from.
///
/// This class only wires AiTools' methods up as AIFunctions and drives the
/// agent loop — the permission checks and Application-service calls live
/// in AiTools itself, kept separate specifically so that logic is unit
/// testable without any LLM/agent framework involved (see AiTools.cs).
/// </summary>
public sealed class GeminiAiAssistantService(IChatClient chatClient, AiTools tools) : IAiAssistantService
{
    public async Task<AssistantAnswer> AskAsync(string question, CancellationToken ct)
    {
        var citations = new List<DocumentCitation>();

        var aiTools = new List<AITool>
        {
            AIFunctionFactory.Create(
                ([Description("Search text — part of a name, SKU, or barcode.")] string query, CancellationToken innerCt)
                    => tools.SearchProductsAsync(query, innerCt),
                name: "search_products",
                description: "Search the product catalog by name, SKU, or barcode. Requires catalog.read."),
            AIFunctionFactory.Create(
                ([Description("Optional search text to filter by product name/SKU.")] string? search, CancellationToken innerCt)
                    => tools.GetInventoryAsync(search, innerCt),
                name: "get_inventory",
                description: "Get current stock levels, optionally filtered by a search term. Requires inventory.read."),
            AIFunctionFactory.Create(
                (CancellationToken innerCt) => tools.GetLowStockItemsAsync(innerCt),
                name: "get_low_stock_items",
                description: "List products currently at or below their low-stock threshold. Requires inventory.read."),
            AIFunctionFactory.Create(
                ([Description("Start date, ISO 8601 (e.g. 2026-09-01).")] DateTime fromDate,
                 [Description("End date, ISO 8601 (e.g. 2026-09-19).")] DateTime toDate,
                 CancellationToken innerCt) => tools.GetSalesSummaryAsync(fromDate, toDate, innerCt),
                name: "get_sales_summary",
                description: "Get a day-by-day sales summary (order count, revenue, average order value) between two dates (ISO 8601, e.g. 2026-09-01). Requires reports.view."),
            AIFunctionFactory.Create(
                ([Description("Optional status filter: Draft, Submitted, Approved, PartiallyReceived, Received, or Cancelled.")] string? status, CancellationToken innerCt)
                    => tools.GetPurchaseOrderStatusAsync(status, innerCt),
                name: "get_purchase_order_status",
                description: "List recent purchase orders, optionally filtered by status (Draft, Submitted, Approved, PartiallyReceived, Received, Cancelled). Requires catalog.read."),
            AIFunctionFactory.Create(
                (CancellationToken innerCt) => tools.GetSupplierStatusAsync(innerCt),
                name: "get_supplier_status",
                description: "List suppliers and their active/inactive status. Requires catalog.read."),
            AIFunctionFactory.Create(
                ([Description("Search text to match against a customer's name.")] string? search, CancellationToken innerCt)
                    => tools.GetCustomerSummaryAsync(search, innerCt),
                name: "get_customer_summary",
                description: "Search customers by name. Requires sales.create."),
            AIFunctionFactory.Create(
                ([Description("What to search for in the uploaded documents.")] string query, CancellationToken innerCt)
                    => tools.SearchDocumentsAsync(query, citations, innerCt),
                name: "search_documents",
                description: "Search uploaded store documents (policies, supplier agreements, etc.) for information not available from live business data. Requires ai.assistant.use."),
        };

        var agent = chatClient.AsAIAgent(
            instructions:
                "You are QuickStock's store assistant. Answer using the provided tools — never invent " +
                "inventory, sales, or document content. If a tool returns a permission error, tell the " +
                "user they don't have access rather than guessing an answer. If search_documents finds " +
                "nothing relevant, say so rather than answering from general knowledge. Be concise.",
            tools: aiTools);

        var response = await agent.RunAsync(question, cancellationToken: ct);

        var toolsUsed = response.Messages
            .SelectMany(m => m.Contents.OfType<FunctionCallContent>())
            .Select(f => f.Name)
            .Distinct()
            .ToList();

        return new AssistantAnswer(response.Text, toolsUsed, citations);
    }
}
