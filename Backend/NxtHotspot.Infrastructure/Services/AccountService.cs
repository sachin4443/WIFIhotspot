using Microsoft.EntityFrameworkCore;
using NxtHotspot.Application.Common;
using NxtHotspot.Application.Dtos;
using NxtHotspot.Infrastructure.Persistence;

namespace NxtHotspot.Infrastructure.Services;

public sealed class AccountService : IAccountService
{
    private readonly NxtDbContext _db;
    private readonly ICurrentUser _current;

    public AccountService(NxtDbContext db, ICurrentUser current) { _db = db; _current = current; }

    public async Task<AccountContextDto> GetContextAsync(CancellationToken ct = default)
    {
        var adminId = _current.AdminUserId ?? throw new UnauthorizedAppException("Not signed in.");
        var user = await _db.AdminUsers.AsNoTracking()
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == adminId, ct) ?? throw new UnauthorizedAppException("Account not found.");
        if (!user.IsActive) throw new UnauthorizedAppException("Account is disabled.");

        AccountOrganizationDto? org = null;
        var sites = new List<AccountSiteDto>();
        if (user.OrganizationId is { } orgId)
        {
            org = await _db.Organizations.AsNoTracking().Where(o => o.Id == orgId)
                .Select(o => new AccountOrganizationDto(o.Id, o.Name, o.OrganizationTypeId, o.OrganizationType!.TypeName, o.Status))
                .FirstOrDefaultAsync(ct);

            var q = _db.Sites.AsNoTracking().Where(s => s.OrganizationId == orgId);
            if (!user.HasAllSitesAccess)
                q = q.Where(s => _db.AdminUserSites.Any(x => x.AdminUserId == adminId && x.SiteId == s.Id));
            sites = await q.OrderByDescending(s => s.IsPrimary).ThenBy(s => s.Name)
                .Select(s => new AccountSiteDto(s.Id, s.Name, s.Code, s.IsPrimary, s.IsActive)).ToListAsync(ct);
        }

        return new AccountContextDto(user.Id, user.Username, user.FullName, user.OrganizationId == null, user.HasAllSitesAccess,
            org, sites, user.UserRoles.Select(r => r.Role!.Name).OrderBy(n => n).ToList(),
            _current.Permissions.OrderBy(p => p, StringComparer.Ordinal).ToList());
    }
}
