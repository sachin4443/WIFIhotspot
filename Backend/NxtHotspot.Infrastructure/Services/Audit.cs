using NxtHotspot.Domain.Entities;

namespace NxtHotspot.Infrastructure.Services;

internal static class Audit
{
    public static AuditLog Entry(Guid? organizationId, Guid? adminUserId, string action, string entityType,
        string? entityId, string? details = null, string? ip = null) => new()
    {
        OrganizationId = organizationId,
        AdminUserId = adminUserId,
        Action = action,
        EntityType = entityType,
        EntityId = entityId,
        // Callers must never pass passwords, API keys or OTPs here.
        Details = details,
        IpAddress = ip
    };
}
