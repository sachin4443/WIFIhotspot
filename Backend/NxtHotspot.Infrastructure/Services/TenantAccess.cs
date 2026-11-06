using Microsoft.EntityFrameworkCore;
using NxtHotspot.Application.Common;
using NxtHotspot.Domain.Entities;
using NxtHotspot.Infrastructure.Persistence;

namespace NxtHotspot.Infrastructure.Services;

public sealed class TenantAccess : ITenantAccess
{
    private readonly NxtDbContext _db;
    private readonly ICurrentUser _current;

    public TenantAccess(NxtDbContext db, ICurrentUser current) { _db = db; _current = current; }

    public void EnsureOrganization(Guid organizationId)
    {
        if (_current.IsPlatformAdmin) return;
        if (_current.OrganizationId != organizationId) throw new ForbiddenException();
    }

    public async Task<Site> GetAuthorizedSiteAsync(Guid siteId, CancellationToken ct = default)
    {
        var site = await _db.Sites.FirstOrDefaultAsync(s => s.Id == siteId, ct);
        // NotFound (not Forbidden) for other tenants' sites so existence is not revealed.
        if (site == null) throw new NotFoundException("Site not found.");
        if (_current.IsPlatformAdmin) return site;

        if (_current.OrganizationId != site.OrganizationId) throw new NotFoundException("Site not found.");

        if (!_current.HasAllSites)
        {
            var adminId = _current.AdminUserId;
            var allowed = adminId != null &&
                          await _db.AdminUserSites.AnyAsync(x => x.AdminUserId == adminId && x.SiteId == siteId, ct);
            if (!allowed) throw new NotFoundException("Site not found.");
        }
        return site;
    }
}
