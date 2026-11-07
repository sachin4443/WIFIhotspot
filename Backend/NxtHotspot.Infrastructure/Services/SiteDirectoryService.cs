using Microsoft.EntityFrameworkCore;
using NxtHotspot.Application.Common;
using NxtHotspot.Application.Dtos;
using NxtHotspot.Infrastructure.Persistence;

namespace NxtHotspot.Infrastructure.Services;

/// <summary>Cross-organization site and wallet listings. Organization admins only ever see their own organization (and sites).</summary>
public sealed class SiteDirectoryService : ISiteDirectoryService
{
    private readonly NxtDbContext _db;
    private readonly ICurrentUser _current;
    private readonly ITenantAccess _tenant;

    public SiteDirectoryService(NxtDbContext db, ICurrentUser current, ITenantAccess tenant) { _db = db; _current = current; _tenant = tenant; }

    private async Task<(Guid? OrgId, IReadOnlyList<Guid>? SiteIds)> ScopeAsync(Guid? organizationId, CancellationToken ct)
    {
        if (_current.IsPlatformAdmin) return (organizationId, null);
        var own = _current.OrganizationId ?? throw new ForbiddenException();
        if (organizationId != null && organizationId != own) throw new ForbiddenException();
        return (own, await _tenant.AccessibleSiteIdsAsync(own, ct));
    }

    public async Task<PagedResult<SiteListItemDto>> ListAsync(Guid? organizationId, string? search, bool? isActive, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var (orgId, siteIds) = await ScopeAsync(organizationId, ct);

        var q = _db.Sites.AsNoTracking().AsQueryable();
        if (orgId != null) q = q.Where(s => s.OrganizationId == orgId);
        if (siteIds != null) q = q.Where(s => siteIds.Contains(s.Id));
        if (isActive != null) q = q.Where(s => s.IsActive == isActive);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var t = search.Trim().ToLower();
            q = q.Where(s => s.Name.ToLower().Contains(t) || s.Code.ToLower().Contains(t) || (s.City != null && s.City.ToLower().Contains(t))
                             || s.Organization!.Name.ToLower().Contains(t));
        }

        var total = await q.CountAsync(ct);
        var items = await q.OrderBy(s => s.Organization!.Name).ThenByDescending(s => s.IsPrimary).ThenBy(s => s.Name)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(s => new SiteListItemDto(s.Id, s.OrganizationId, s.Organization!.Name, s.Name, s.Code, s.City, s.IsPrimary, s.IsActive,
                _db.SiteWallets.Where(w => w.SiteId == s.Id).Select(w => w.Balance).FirstOrDefault(), s.CreatedAt))
            .ToListAsync(ct);
        return new PagedResult<SiteListItemDto>(items, page, pageSize, total);
    }

    public async Task<PagedResult<WalletOverviewDto>> ListWalletsAsync(Guid? organizationId, string? search, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var (orgId, siteIds) = await ScopeAsync(organizationId, ct);

        var q = _db.SiteWallets.AsNoTracking().AsQueryable();
        if (orgId != null) q = q.Where(w => w.OrganizationId == orgId);
        if (siteIds != null) q = q.Where(w => siteIds.Contains(w.SiteId));
        if (!string.IsNullOrWhiteSpace(search))
        {
            var t = search.Trim().ToLower();
            q = q.Where(w => w.Site!.Name.ToLower().Contains(t) || w.Site.Code.ToLower().Contains(t) || w.Site.Organization!.Name.ToLower().Contains(t));
        }

        var total = await q.CountAsync(ct);
        var items = await q.OrderBy(w => w.Site!.Organization!.Name).ThenBy(w => w.Site!.Name)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(w => new WalletOverviewDto(w.Id, w.SiteId, w.Site!.Name, w.Site.Code, w.OrganizationId, w.Site.Organization!.Name,
                w.Balance, w.Currency, w.IsActive, w.UpdatedAt))
            .ToListAsync(ct);
        return new PagedResult<WalletOverviewDto>(items, page, pageSize, total);
    }
}
