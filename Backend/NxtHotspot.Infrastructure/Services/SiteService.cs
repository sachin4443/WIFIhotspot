using Microsoft.EntityFrameworkCore;
using NxtHotspot.Application.Common;
using NxtHotspot.Application.Dtos;
using NxtHotspot.Domain.Entities;
using NxtHotspot.Domain.Enums;
using NxtHotspot.Infrastructure.Persistence;

namespace NxtHotspot.Infrastructure.Services;

public sealed class SiteService : ISiteService
{
    private readonly NxtDbContext _db;
    private readonly ICurrentUser _current;
    private readonly ITenantAccess _tenant;

    public SiteService(NxtDbContext db, ICurrentUser current, ITenantAccess tenant)
    {
        _db = db; _current = current; _tenant = tenant;
    }

    public async Task<List<SiteDto>> ListAsync(Guid organizationId, CancellationToken ct = default)
    {
        _tenant.EnsureOrganization(organizationId);
        var q = _db.Sites.AsNoTracking().Where(s => s.OrganizationId == organizationId);

        if (!_current.IsPlatformAdmin && !_current.HasAllSites)
        {
            var adminId = _current.AdminUserId;
            q = q.Where(s => _db.AdminUserSites.Any(x => x.AdminUserId == adminId && x.SiteId == s.Id));
        }

        return await q.OrderByDescending(s => s.IsPrimary).ThenBy(s => s.Name).Select(s => Map(s)).ToListAsync(ct);
    }

    public async Task<SiteDto> CreateAsync(Guid organizationId, CreateSiteRequest request, CancellationToken ct = default)
    {
        _tenant.EnsureOrganization(organizationId);
        if (!_current.IsPlatformAdmin && !_current.HasAllSites)
            throw new ForbiddenException("Only administrators with access to all sites can create sites.");

        var org = await _db.Organizations.Include(o => o.SubscriptionPlan).FirstOrDefaultAsync(o => o.Id == organizationId, ct)
                  ?? throw new NotFoundException("Organization not found.");
        if (org.Status is OrganizationStatus.Suspended or OrganizationStatus.Inactive)
            throw new ConflictException("Sites cannot be added while the organization is suspended or inactive.");

        var code = request.Code.Trim().ToUpperInvariant();
        if (await _db.Sites.AnyAsync(s => s.OrganizationId == organizationId && s.Code == code, ct))
            throw new ConflictException("A site with this code already exists.");

        var limit = org.SubscriptionPlan?.MaxSites;
        if (limit != null && await _db.Sites.CountAsync(s => s.OrganizationId == organizationId, ct) >= limit)
            throw new ConflictException("The subscription plan's site limit has been reached.");

        var site = new Site
        {
            OrganizationId = organizationId,
            Name = request.Name.Trim(),
            Code = code,
            Address = request.Address?.Trim(),
            City = request.City?.Trim(),
            IsPrimary = false
        };
        _db.Sites.Add(site);
        _db.SiteWallets.Add(new SiteWallet { OrganizationId = organizationId, SiteId = site.Id, Balance = 0 });
        _db.AuditLogs.Add(Audit.Entry(organizationId, _current.AdminUserId, "SiteCreated", nameof(Site), site.Id.ToString(), $"Code={code}"));

        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { throw new ConflictException("A site with this code already exists."); }
        return Map(site);
    }

    public async Task<SiteDto> UpdateAsync(Guid siteId, UpdateSiteRequest request, CancellationToken ct = default)
    {
        var site = await _tenant.GetAuthorizedSiteAsync(siteId, ct);
        if (!request.IsActive && site.IsPrimary)
            throw new ValidationFailedException("The primary site cannot be deactivated.");

        site.Name = request.Name.Trim();
        site.Address = request.Address?.Trim();
        site.City = request.City?.Trim();
        site.IsActive = request.IsActive;
        _db.AuditLogs.Add(Audit.Entry(site.OrganizationId, _current.AdminUserId, "SiteUpdated", nameof(Site), site.Id.ToString()));
        await _db.SaveChangesAsync(ct);
        return Map(site);
    }

    private static SiteDto Map(Site s) => new(s.Id, s.OrganizationId, s.Name, s.Code, s.Address, s.City, s.IsPrimary, s.IsActive);
}
