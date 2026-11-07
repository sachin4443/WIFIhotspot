using Microsoft.EntityFrameworkCore;
using NxtHotspot.Application.Common;
using NxtHotspot.Infrastructure.Persistence;

namespace NxtHotspot.Infrastructure.Services;

/// <summary>Tenant and site checks shared by the hotspot services.</summary>
internal static class HotspotScope
{
    /// <summary>Throws unless the caller may change an object at this site (or organization-wide when siteId is null).</summary>
    public static async Task EnsureWritableAsync(ITenantAccess tenant, ICurrentUser current, Guid organizationId, Guid? siteId, CancellationToken ct)
    {
        tenant.EnsureOrganization(organizationId);
        if (siteId is { } sid)
        {
            var site = await tenant.GetAuthorizedSiteAsync(sid, ct);
            if (site.OrganizationId != organizationId) throw new NotFoundException("Site not found.");
            return;
        }
        if (!current.IsPlatformAdmin && !current.HasAllSites)
            throw new ForbiddenException("Only administrators with access to all sites can manage organization-wide items.");
    }

    public static async Task EnsureOrganizationExistsAsync(NxtDbContext db, Guid organizationId, CancellationToken ct)
    {
        if (!await db.Organizations.AnyAsync(o => o.Id == organizationId, ct)) throw new NotFoundException("Organization not found.");
    }
}
