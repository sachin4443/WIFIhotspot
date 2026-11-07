using Microsoft.EntityFrameworkCore;
using NxtHotspot.Application.Common;
using NxtHotspot.Application.Dtos;
using NxtHotspot.Domain.Entities;
using NxtHotspot.Infrastructure.Persistence;

namespace NxtHotspot.Infrastructure.Services;

public sealed class SubscriberGroupService : ISubscriberGroupService
{
    private readonly NxtDbContext _db;
    private readonly ICurrentUser _current;
    private readonly ITenantAccess _tenant;

    public SubscriberGroupService(NxtDbContext db, ICurrentUser current, ITenantAccess tenant) { _db = db; _current = current; _tenant = tenant; }

    public async Task<List<SubscriberGroupDto>> ListAsync(Guid organizationId, CancellationToken ct = default)
    {
        _tenant.EnsureOrganization(organizationId);
        return await _db.SubscriberGroups.AsNoTracking().Where(g => g.OrganizationId == organizationId)
            .OrderBy(g => g.GroupType).ThenBy(g => g.Name)
            .Select(g => new SubscriberGroupDto(g.Id, g.OrganizationId, g.Name, g.GroupType, g.Description, g.DefaultPackageId,
                g.DefaultPackage != null ? g.DefaultPackage.Name : null, g.IsActive, _db.Subscribers.Count(s => s.GroupId == g.Id)))
            .ToListAsync(ct);
    }

    public async Task<SubscriberGroupDto> CreateAsync(Guid organizationId, UpsertGroupRequest request, CancellationToken ct = default)
    {
        await HotspotScope.EnsureWritableAsync(_tenant, _current, organizationId, null, ct);
        await HotspotScope.EnsureOrganizationExistsAsync(_db, organizationId, ct);
        await ValidateAsync(organizationId, request, null, ct);
        var g = new SubscriberGroup { OrganizationId = organizationId };
        Apply(g, request);
        _db.SubscriberGroups.Add(g);
        _db.AuditLogs.Add(Audit.Entry(organizationId, _current.AdminUserId, "SubscriberGroupCreated", nameof(SubscriberGroup), g.Id.ToString(), $"{g.GroupType}={g.Name}"));
        await _db.SaveChangesAsync(ct);
        return (await ListAsync(organizationId, ct)).First(x => x.Id == g.Id);
    }

    public async Task<SubscriberGroupDto> UpdateAsync(Guid organizationId, Guid groupId, UpsertGroupRequest request, CancellationToken ct = default)
    {
        await HotspotScope.EnsureWritableAsync(_tenant, _current, organizationId, null, ct);
        var g = await _db.SubscriberGroups.FirstOrDefaultAsync(x => x.Id == groupId && x.OrganizationId == organizationId, ct)
                ?? throw new NotFoundException("Group not found.");
        await ValidateAsync(organizationId, request, groupId, ct);
        Apply(g, request);
        _db.AuditLogs.Add(Audit.Entry(organizationId, _current.AdminUserId, "SubscriberGroupUpdated", nameof(SubscriberGroup), g.Id.ToString(), $"{g.GroupType}={g.Name}"));
        await _db.SaveChangesAsync(ct);
        return (await ListAsync(organizationId, ct)).First(x => x.Id == g.Id);
    }

    public async Task DeleteAsync(Guid organizationId, Guid groupId, CancellationToken ct = default)
    {
        await HotspotScope.EnsureWritableAsync(_tenant, _current, organizationId, null, ct);
        var g = await _db.SubscriberGroups.FirstOrDefaultAsync(x => x.Id == groupId && x.OrganizationId == organizationId, ct)
                ?? throw new NotFoundException("Group not found.");
        if (await _db.Subscribers.AnyAsync(s => s.GroupId == groupId, ct))
            throw new ConflictException("Subscribers belong to this group. Move them first or deactivate the group.");
        _db.SubscriberGroups.Remove(g);
        _db.AuditLogs.Add(Audit.Entry(organizationId, _current.AdminUserId, "SubscriberGroupDeleted", nameof(SubscriberGroup), g.Id.ToString(), g.Name));
        await _db.SaveChangesAsync(ct);
    }

    private async Task ValidateAsync(Guid organizationId, UpsertGroupRequest r, Guid? exceptId, CancellationToken ct)
    {
        var name = r.Name.Trim().ToLower();
        if (await _db.SubscriberGroups.AnyAsync(g => g.OrganizationId == organizationId && g.Id != exceptId && g.GroupType == r.GroupType && g.Name.ToLower() == name, ct))
            throw new ConflictException($"A {r.GroupType.ToString().ToLower()} with this name already exists.");
        if (r.DefaultPackageId is { } pid && !await _db.InternetPackages.AnyAsync(p => p.Id == pid && p.OrganizationId == organizationId, ct))
            throw new ValidationFailedException("The default package does not belong to this organization.");
    }

    private static void Apply(SubscriberGroup g, UpsertGroupRequest r)
    {
        g.Name = r.Name.Trim();
        g.GroupType = r.GroupType;
        g.Description = string.IsNullOrWhiteSpace(r.Description) ? null : r.Description.Trim();
        g.DefaultPackageId = r.DefaultPackageId;
        g.IsActive = r.IsActive;
    }
}
