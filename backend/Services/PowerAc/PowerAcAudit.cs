/*
 * File: PowerAcAudit.cs
 * Appends entries to dbo.PowerAcAuditLog. The caller saves the DbContext.
 */
using System.Text.Json;
using backend.Data;
using backend.Models.PowerAc;

namespace backend.Services.PowerAc
{
    public static class PowerAcAudit
    {
        public static PowerAcAuditLog Add(
            AppDbContext db,
            string entityType,
            int entityId,
            string action,
            string? fromStatus,
            string? toStatus,
            string? changedBy,
            object? details = null,
            string? comment = null)
        {
            var entry = new PowerAcAuditLog
            {
                EntityType = entityType,
                EntityId = entityId,
                Action = action,
                FromStatus = fromStatus,
                ToStatus = toStatus,
                DetailsJson = details is null ? null : JsonSerializer.Serialize(details),
                Comment = comment is null ? null : (comment.Length > 500 ? comment[..500] : comment),
                ChangedBy = changedBy,
                ChangedAt = DateTime.UtcNow
            };
            db.PowerAcAuditLogs.Add(entry);
            return entry;
        }
    }
}
