using Microsoft.EntityFrameworkCore;
using NxtHotspot.Application.Common;
using NxtHotspot.Application.Dtos;
using NxtHotspot.Domain.Entities;
using NxtHotspot.Infrastructure.Persistence;

namespace NxtHotspot.Infrastructure.Services;

public sealed class RoleService : IRoleService
{
    private readonly NxtDbContext _db;
    private readonly ICurrentUser _current;

    public RoleService(NxtDbContext db, ICurrentUser current) { _db = db; _current = current; }

    public async Task<List<RoleDto>> ListRolesAsync(CancellationToken ct = default)
    {
        var q = _db.Roles.AsNoTracking().Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission).AsQueryable();
        if (!_current.IsPlatformAdmin)
        {
            var org = _current.OrganizationId ?? throw new ForbiddenException();
            q = q.Where(r => (r.OrganizationId == null && !r.IsPlatformRole) || r.OrganizationId == org);
        }
        var roles = await q.OrderBy(r => r.Name).ToListAsync(ct);
        return roles.Select(Map).ToList();
    }

    public async Task<List<PermissionDto>> ListPermissionsAsync(CancellationToken ct = default)
    {
        var q = _db.Permissions.AsNoTracking().AsQueryable();
        if (!_current.IsPlatformAdmin) q = q.Where(p => !p.IsPlatformOnly);
        return await q.OrderBy(p => p.Code).Select(p => new PermissionDto(p.Id, p.Code, p.Description, p.IsPlatformOnly)).ToListAsync(ct);
    }

    public async Task<RoleDto> CreateRoleAsync(CreateRoleRequest req, CancellationToken ct = default)
    {
        Guid? orgId;
        if (_current.IsPlatformAdmin) orgId = req.OrganizationId;
        else
        {
            orgId = _current.OrganizationId ?? throw new ForbiddenException();
            if (req.OrganizationId != null && req.OrganizationId != orgId) throw new ForbiddenException();
        }

        var name = req.Name.Trim();
        if (await _db.Roles.AnyAsync(r => r.OrganizationId == orgId && r.Name == name, ct))
            throw new ConflictException("A role with this name already exists.");

        var perms = await ResolvePermissionsAsync(req.PermissionCodes, orgId, ct);
        var role = new Role
        {
            OrganizationId = orgId,
            Name = name,
            Description = req.Description?.Trim(),
            IsSystem = false,
            // A global role containing platform-only permissions must never be assignable by org admins.
            IsPlatformRole = orgId == null && perms.Any(p => p.IsPlatformOnly)
        };
        foreach (var p in perms) role.RolePermissions.Add(new RolePermission { PermissionId = p.Id });

        _db.Roles.Add(role);
        _db.AuditLogs.Add(Audit.Entry(orgId, _current.AdminUserId, "RoleCreated", nameof(Role), role.Id.ToString(), $"Name={name}"));
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { throw new ConflictException("A role with this name already exists."); }

        return await GetDtoAsync(role.Id, ct);
    }

    public async Task<RoleDto> SetRolePermissionsAsync(Guid roleId, IReadOnlyCollection<string> permissionCodes, CancellationToken ct = default)
    {
        var role = await LoadMutableRoleAsync(roleId, ct);
        var perms = await ResolvePermissionsAsync(permissionCodes, role.OrganizationId, ct);
        var wanted = perms.Select(p => p.Id).ToHashSet();

        foreach (var rp in role.RolePermissions.Where(rp => !wanted.Contains(rp.PermissionId)).ToList())
            role.RolePermissions.Remove(rp);
        foreach (var id in wanted.Where(id => role.RolePermissions.All(rp => rp.PermissionId != id)))
            role.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = id });

        if (role.OrganizationId == null) role.IsPlatformRole = perms.Any(p => p.IsPlatformOnly);

        _db.AuditLogs.Add(Audit.Entry(role.OrganizationId, _current.AdminUserId, "RolePermissionsChanged", nameof(Role), role.Id.ToString()));
        await _db.SaveChangesAsync(ct);
        return await GetDtoAsync(role.Id, ct);
    }

    public async Task DeleteRoleAsync(Guid roleId, CancellationToken ct = default)
    {
        var role = await LoadMutableRoleAsync(roleId, ct);
        if (await _db.AdminUserRoles.AnyAsync(ur => ur.RoleId == roleId, ct))
            throw new ConflictException("The role is assigned to users. Remove it from them first.");

        _db.Roles.Remove(role);
        _db.AuditLogs.Add(Audit.Entry(role.OrganizationId, _current.AdminUserId, "RoleDeleted", nameof(Role), role.Id.ToString(), $"Name={role.Name}"));
        await _db.SaveChangesAsync(ct);
    }

    // ---------- helpers ----------

    private async Task<Role> LoadMutableRoleAsync(Guid roleId, CancellationToken ct)
    {
        var role = await _db.Roles.Include(r => r.RolePermissions).FirstOrDefaultAsync(r => r.Id == roleId, ct)
                   ?? throw new NotFoundException("Role not found.");
        // Non-platform system roles are visible to every tenant, so report them as immutable (409) rather than hiding them.
        if (role.IsSystem && !role.IsPlatformRole)
            throw new ConflictException("System roles cannot be modified or deleted.");
        // Anything else belonging to another tenant (or a platform role) is reported as not found.
        if (!_current.IsPlatformAdmin && role.OrganizationId != _current.OrganizationId)
            throw new NotFoundException("Role not found.");
        if (role.IsSystem) throw new ConflictException("System roles cannot be modified or deleted.");
        return role;
    }

    private async Task<List<Permission>> ResolvePermissionsAsync(IReadOnlyCollection<string> codes, Guid? orgId, CancellationToken ct)
    {
        var distinct = codes.Select(c => c.Trim()).Where(c => c.Length > 0).Distinct().ToList();
        if (distinct.Count == 0) return new List<Permission>();

        var perms = await _db.Permissions.Where(p => distinct.Contains(p.Code)).ToListAsync(ct);
        if (perms.Count != distinct.Count) throw new ValidationFailedException("One or more permission codes are unknown.");

        if (orgId != null && perms.Any(p => p.IsPlatformOnly))
            throw new ValidationFailedException("Platform-only permissions cannot be granted to organization roles.");

        if (!_current.IsPlatformAdmin && perms.Any(p => !_current.Permissions.Contains(p.Code)))
            throw new ForbiddenException("You cannot grant permissions you do not hold.");

        return perms;
    }

    private async Task<RoleDto> GetDtoAsync(Guid id, CancellationToken ct) =>
        Map(await _db.Roles.AsNoTracking().Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission).FirstAsync(r => r.Id == id, ct));

    private static RoleDto Map(Role r) => new(r.Id, r.OrganizationId, r.Name, r.Description, r.IsSystem, r.IsPlatformRole,
        r.RolePermissions.Where(rp => rp.Permission != null).Select(rp => rp.Permission!.Code).OrderBy(c => c).ToList());
}
