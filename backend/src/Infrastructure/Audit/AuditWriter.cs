using System.Text.Json;
using Application.Common;
using Domain.Audit;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;

namespace Infrastructure.Audit;

public sealed class AuditWriter(GroceryDbContext db, ITenantContext tenantContext, IHttpContextAccessor httpContextAccessor) : IAuditWriter
{
    public void Record(string action, string entityType, string entityId, object? metadata = null)
    {
        var metadataJson = metadata is null ? null : JsonSerializer.Serialize(metadata);
        var correlationId = httpContextAccessor.HttpContext?.TraceIdentifier;

        var entry = new AuditLogEntry(
            tenantContext.IsAuthenticated ? tenantContext.StoreId : null,
            tenantContext.UserId,
            action,
            entityType,
            entityId,
            metadataJson,
            correlationId);

        db.AuditLogEntries.Add(entry);
    }
}
