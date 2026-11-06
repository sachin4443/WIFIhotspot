using Microsoft.EntityFrameworkCore;
using NxtHotspot.Application.Common;
using NxtHotspot.Application.Dtos;
using NxtHotspot.Domain.Entities;
using NxtHotspot.Infrastructure.Persistence;

namespace NxtHotspot.Infrastructure.Services;

public sealed class AdminUserService : IAdminUserService
{
    private readonly NxtDbContext _db;
    private readonly IPasswordService _passwords;
    private readonly ICurrentUser _current;

    public AdminUserService(NxtDbContext db, IPasswordService passwords, ICurrentUser current)
    {
        _db = db; _passwords = passwords; _current = current;
    }

    public async Task<List<AdminUserDto>> ListAsync(Guid? organizationId, CancellationToken ct = default)
    {
        var q = _db.AdminUsers.AsNoTracking().Include(u => u.UserRoles).ThenInclude(ur => ur.Role).Include(u => u.UserSites).AsQueryable();

        if (_current.IsPlatformAdmin)
        {
            if (organizationId != null) q = q.Where(u => u.OrganizationId == organizationId);
        }
        else
        {
            var own = _current.OrganizationId ?? throw new ForbiddenException();
            if (organizationId != null && organizationId != own) throw new ForbiddenException();
            q = q.Where(u => u.OrganizationId == own);
        }

        var users = await q.OrderBy(u => u.Username).ToListAsync(ct);
        return users.Select(Map).ToList();
    }

    public async Task<AdminUserDto> CreateAsync(CreateAdminUserRequest req, CancellationToken ct = default)
    {
        Guid? orgId;
        if (_current.IsPlatformAdmin) orgId = req.OrganizationId;
        else
        {
            orgId = _current.OrganizationId ?? throw new ForbiddenException();
            if (req.OrganizationId != null && req.OrganizationId != orgId) throw new ForbiddenException();
        }

        if (orgId != null && !await _db.Organizations.AnyAsync(o => o.Id == orgId, ct))
            throw new NotFoundException("Organization not found.");

        var passwordError = PasswordPolicy.Validate(req.Password);
        if (passwordError != null) throw new ValidationFailedException(passwordError);

        var username = req.Username.Trim();
        var normalized = username.ToUpperInvariant();
        if (await _db.AdminUsers.AnyAsync(u => u.NormalizedUsername == normalized, ct))
            throw new ConflictException("Username is already taken.");

        if (req.HasAllSitesAccess && !_current.IsPlatformAdmin && !_current.HasAllSites)
            throw new ForbiddenException("You cannot grant access to all sites.");

        var roles = await LoadAssignableRolesAsync(req.RoleIds, orgId, ct);
        var siteIds = req.SiteIds.Distinct().ToList();
        await ValidateSitesAsync(siteIds, orgId, ct);

        var user = new AdminUser
        {
            OrganizationId = orgId,
            Username = username,
            NormalizedUsername = normalized,
            PasswordHash = _passwords.Hash(req.Password),
            FullName = req.FullName.Trim(),
            Email = req.Email.Trim(),
            MobileNumber = req.MobileNumber?.Trim(),
            HasAllSitesAccess = req.HasAllSitesAccess || (orgId == null && _current.IsPlatformAdmin)
        };
        foreach (var r in roles) user.UserRoles.Add(new AdminUserRole { RoleId = r.Id });
        foreach (var s in siteIds) user.UserSites.Add(new AdminUserSite { SiteId = s });

        _db.AdminUsers.Add(user);
        _db.AuditLogs.Add(Audit.Entry(orgId, _current.AdminUserId, "AdminUserCreated", nameof(AdminUser), user.Id.ToString(),
            $"Username={username}"));
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { throw new ConflictException("Username is already taken."); }

        return await GetDtoAsync(user.Id, ct);
    }

    public async Task<AdminUserDto> SetRolesAsync(Guid adminUserId, IReadOnlyCollection<Guid> roleIds, CancellationToken ct = default)
    {
        if (adminUserId == _current.AdminUserId) throw new ValidationFailedException("You cannot change your own roles.");
        var user = await LoadUserAuthorizedAsync(adminUserId, ct);
        var wanted = (await LoadAssignableRolesAsync(roleIds.Distinct().ToList(), user.OrganizationId, ct)).Select(r => r.Id).ToHashSet();

        foreach (var ur in user.UserRoles.Where(ur => !wanted.Contains(ur.RoleId)).ToList())
            user.UserRoles.Remove(ur);
        foreach (var id in wanted.Where(id => user.UserRoles.All(ur => ur.RoleId != id)))
            user.UserRoles.Add(new AdminUserRole { AdminUserId = user.Id, RoleId = id });

        _db.AuditLogs.Add(Audit.Entry(user.OrganizationId, _current.AdminUserId, "AdminUserRolesChanged", nameof(AdminUser), user.Id.ToString()));
        await _db.SaveChangesAsync(ct);
        return await GetDtoAsync(user.Id, ct);
    }

    public async Task<AdminUserDto> SetSitesAsync(Guid adminUserId, IReadOnlyCollection<Guid> siteIds, bool hasAllSites, CancellationToken ct = default)
    {
        var user = await LoadUserAuthorizedAsync(adminUserId, ct);
        if (hasAllSites && !_current.IsPlatformAdmin && !_current.HasAllSites)
            throw new ForbiddenException("You cannot grant access to all sites.");

        var wanted = siteIds.Distinct().ToList();
        await ValidateSitesAsync(wanted, user.OrganizationId, ct);

        user.HasAllSitesAccess = hasAllSites;
        foreach (var us in user.UserSites.Where(us => !wanted.Contains(us.SiteId)).ToList())
            user.UserSites.Remove(us);
        foreach (var id in wanted.Where(id => user.UserSites.All(us => us.SiteId != id)))
            user.UserSites.Add(new AdminUserSite { AdminUserId = user.Id, SiteId = id });

        _db.AuditLogs.Add(Audit.Entry(user.OrganizationId, _current.AdminUserId, "AdminUserSitesChanged", nameof(AdminUser), user.Id.ToString()));
        await _db.SaveChangesAsync(ct);
        return await GetDtoAsync(user.Id, ct);
    }

    public async Task SetActiveAsync(Guid adminUserId, bool isActive, CancellationToken ct = default)
    {
        if (adminUserId == _current.AdminUserId && !isActive)
            throw new ValidationFailedException("You cannot deactivate your own account.");
        var user = await LoadUserAuthorizedAsync(adminUserId, ct);
        user.IsActive = isActive;
        _db.AuditLogs.Add(Audit.Entry(user.OrganizationId, _current.AdminUserId,
            isActive ? "AdminUserActivated" : "AdminUserDeactivated", nameof(AdminUser), user.Id.ToString()));
        await _db.SaveChangesAsync(ct);
    }

    // ---------- helpers ----------

    private async Task<AdminUser> LoadUserAuthorizedAsync(Guid id, CancellationToken ct)
    {
        var user = await _db.AdminUsers.Include(u => u.UserRoles).Include(u => u.UserSites)
                       .FirstOrDefaultAsync(u => u.Id == id, ct)
                   ?? throw new NotFoundException("Admin user not found.");

        if (_current.IsPlatformAdmin) return user;
        if (user.OrganizationId == null || user.OrganizationId != _current.OrganizationId)
            throw new NotFoundException("Admin user not found.");
        // A site-restricted admin may not manage users who have organization-wide access.
        if (!_current.HasAllSites && user.HasAllSitesAccess)
            throw new ForbiddenException();
        return user;
    }

    private async Task<List<Role>> LoadAssignableRolesAsync(IReadOnlyCollection<Guid> roleIds, Guid? orgId, CancellationToken ct)
    {
        var ids = roleIds.Distinct().ToList();
        if (ids.Count == 0) return new List<Role>();

        var roles = await _db.Roles.Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
            .Where(r => ids.Contains(r.Id)).ToListAsync(ct);
        if (roles.Count != ids.Count) throw new ValidationFailedException("One or more roles do not exist.");

        foreach (var role in roles)
        {
            if (role.IsPlatformRole)
            {
                if (!_current.IsPlatformAdmin) throw new ForbiddenException("Only platform administrators can assign platform roles.");
                if (orgId != null) throw new ValidationFailedException("Platform roles cannot be assigned to organization users.");
            }
            else
            {
                if (orgId == null) throw new ValidationFailedException("Organization roles cannot be assigned to platform users.");
                if (role.OrganizationId != null && role.OrganizationId != orgId)
                    throw new ValidationFailedException("A role does not belong to this organization.");
            }

            // Privilege-escalation guard: you can only hand out permissions you hold yourself.
            if (!_current.IsPlatformAdmin &&
                role.RolePermissions.Any(rp => !_current.Permissions.Contains(rp.Permission!.Code)))
                throw new ForbiddenException("You cannot assign a role containing permissions you do not hold.");
        }
        return roles;
    }

    private async Task ValidateSitesAsync(List<Guid> siteIds, Guid? orgId, CancellationToken ct)
    {
        if (siteIds.Count == 0) return;
        if (orgId == null) throw new ValidationFailedException("Platform users cannot be assigned to sites.");

        var count = await _db.Sites.CountAsync(s => siteIds.Contains(s.Id) && s.OrganizationId == orgId, ct);
        if (count != siteIds.Count) throw new ValidationFailedException("One or more sites do not belong to this organization.");

        if (!_current.IsPlatformAdmin && !_current.HasAllSites)
        {
            var adminId = _current.AdminUserId;
            var mine = await _db.AdminUserSites.CountAsync(x => x.AdminUserId == adminId && siteIds.Contains(x.SiteId), ct);
            if (mine != siteIds.Count) throw new ForbiddenException("You can only assign sites you have access to.");
        }
    }

    private async Task<AdminUserDto> GetDtoAsync(Guid id, CancellationToken ct)
    {
        var user = await _db.AdminUsers.AsNoTracking().Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Include(u => u.UserSites).FirstAsync(u => u.Id == id, ct);
        return Map(user);
    }

    private static AdminUserDto Map(AdminUser u) => new(u.Id, u.OrganizationId, u.Username, u.FullName, u.Email, u.MobileNumber,
        u.IsActive, u.HasAllSitesAccess,
        u.UserRoles.Where(ur => ur.Role != null).Select(ur => ur.Role!.Name).OrderBy(n => n).ToList(),
        u.UserSites.Select(us => us.SiteId).ToList(), u.LastLoginAt);
}
