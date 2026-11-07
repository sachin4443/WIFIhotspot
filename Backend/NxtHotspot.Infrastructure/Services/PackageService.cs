using Microsoft.EntityFrameworkCore;
using NxtHotspot.Application.Common;
using NxtHotspot.Application.Dtos;
using NxtHotspot.Domain.Entities;
using NxtHotspot.Domain.Enums;
using NxtHotspot.Infrastructure.Persistence;

namespace NxtHotspot.Infrastructure.Services;

public sealed class PackageService : IPackageService
{
    private readonly NxtDbContext _db;
    private readonly ICurrentUser _current;
    private readonly ITenantAccess _tenant;

    public PackageService(NxtDbContext db, ICurrentUser current, ITenantAccess tenant) { _db = db; _current = current; _tenant = tenant; }

    public async Task<List<InternetPackageDto>> ListAsync(Guid organizationId, Guid? siteId, CancellationToken ct = default)
    {
        var sites = await _tenant.AccessibleSiteIdsAsync(organizationId, ct);
        var q = _db.InternetPackages.AsNoTracking().Where(p => p.OrganizationId == organizationId);
        // Site-limited admins see organization-wide packages plus their own sites' packages.
        if (sites != null) q = q.Where(p => p.SiteId == null || sites.Contains(p.SiteId.Value));
        if (siteId != null) q = q.Where(p => p.SiteId == null || p.SiteId == siteId);
        return await q.OrderByDescending(p => p.IsActive).ThenBy(p => p.Price).ThenBy(p => p.Name)
            .Select(p => new InternetPackageDto(p.Id, p.OrganizationId, p.SiteId, p.Site != null ? p.Site.Name : null, p.Name, p.Description,
                p.DownloadKbps, p.UploadKbps, p.DataLimitMb, p.ValidityMinutes, p.SessionTimeoutMinutes, p.IdleTimeoutMinutes, p.MaxDevices,
                p.Price, p.IsActive, _db.Subscribers.Count(s => s.PackageId == p.Id), p.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<InternetPackageDto> CreateAsync(Guid organizationId, UpsertPackageRequest request, CancellationToken ct = default)
    {
        await HotspotScope.EnsureWritableAsync(_tenant, _current, organizationId, request.SiteId, ct);
        await HotspotScope.EnsureOrganizationExistsAsync(_db, organizationId, ct);
        await EnsureNameFreeAsync(organizationId, request.Name, null, ct);
        var p = new InternetPackage { OrganizationId = organizationId };
        Apply(p, request);
        _db.InternetPackages.Add(p);
        _db.AuditLogs.Add(Audit.Entry(organizationId, _current.AdminUserId, "PackageCreated", nameof(InternetPackage), p.Id.ToString(), $"Name={p.Name}"));
        await SaveAsync(ct);
        return (await ListAsync(organizationId, null, ct)).First(x => x.Id == p.Id);
    }

    public async Task<InternetPackageDto> UpdateAsync(Guid organizationId, Guid packageId, UpsertPackageRequest request, CancellationToken ct = default)
    {
        var p = await LoadWritableAsync(organizationId, packageId, ct);
        await HotspotScope.EnsureWritableAsync(_tenant, _current, organizationId, request.SiteId, ct);
        await EnsureNameFreeAsync(organizationId, request.Name, packageId, ct);
        Apply(p, request);
        _db.AuditLogs.Add(Audit.Entry(organizationId, _current.AdminUserId, "PackageUpdated", nameof(InternetPackage), p.Id.ToString(), $"Name={p.Name}"));
        await SaveAsync(ct);
        return (await ListAsync(organizationId, null, ct)).First(x => x.Id == p.Id);
    }

    public async Task DeleteAsync(Guid organizationId, Guid packageId, CancellationToken ct = default)
    {
        var p = await LoadWritableAsync(organizationId, packageId, ct);
        if (await _db.Subscribers.AnyAsync(s => s.PackageId == packageId && s.Status == SubscriberStatus.Active, ct))
            throw new ConflictException("Active subscribers use this package. Move them to another package or deactivate it instead.");
        _db.InternetPackages.Remove(p);
        _db.AuditLogs.Add(Audit.Entry(organizationId, _current.AdminUserId, "PackageDeleted", nameof(InternetPackage), p.Id.ToString(), $"Name={p.Name}"));
        await _db.SaveChangesAsync(ct);
    }

    private async Task<InternetPackage> LoadWritableAsync(Guid organizationId, Guid id, CancellationToken ct)
    {
        _tenant.EnsureOrganization(organizationId);
        var p = await _db.InternetPackages.FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == organizationId, ct)
                ?? throw new NotFoundException("Package not found.");
        await HotspotScope.EnsureWritableAsync(_tenant, _current, organizationId, p.SiteId, ct);
        return p;
    }

    private async Task EnsureNameFreeAsync(Guid organizationId, string name, Guid? exceptId, CancellationToken ct)
    {
        var n = name.Trim().ToLower();
        if (await _db.InternetPackages.AnyAsync(p => p.OrganizationId == organizationId && p.Id != exceptId && p.Name.ToLower() == n, ct))
            throw new ConflictException("A package with this name already exists.");
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { throw new ConflictException("A package with this name already exists."); }
    }

    private static void Apply(InternetPackage p, UpsertPackageRequest r)
    {
        p.SiteId = r.SiteId;
        p.Name = r.Name.Trim();
        p.Description = string.IsNullOrWhiteSpace(r.Description) ? null : r.Description.Trim();
        p.DownloadKbps = r.DownloadKbps;
        p.UploadKbps = r.UploadKbps;
        p.DataLimitMb = r.DataLimitMb;
        p.ValidityMinutes = r.ValidityMinutes;
        p.SessionTimeoutMinutes = r.SessionTimeoutMinutes;
        p.IdleTimeoutMinutes = r.IdleTimeoutMinutes;
        p.MaxDevices = r.MaxDevices;
        p.Price = r.Price;
        p.IsActive = r.IsActive;
    }
}
