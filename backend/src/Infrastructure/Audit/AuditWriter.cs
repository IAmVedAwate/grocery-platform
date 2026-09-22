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
        var entry = new AuditLogEntry(
            tenantContext.IsAuthenticated ? tenantContext.StoreId : null,
            tenantContext.UserId,
            action,
            entityType,
            entityId,
            Serialize(metadata),
            httpContextAccessor.HttpContext?.TraceIdentifier);

        db.AuditLogEntries.Add(entry);
    }

    public void RecordWithExplicitActor(Guid storeId, Guid actorUserId, string action, string entityType, string entityId, object? metadata = null)
    {
        var entry = new AuditLogEntry(
            storeId, actorUserId, action, entityType, entityId,
            Serialize(metadata),
            httpContextAccessor.HttpContext?.TraceIdentifier);

        db.AuditLogEntries.Add(entry);
    }

    private static string? Serialize(object? metadata) => metadata is null ? null : JsonSerializer.Serialize(metadata);
}
