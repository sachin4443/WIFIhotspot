using Microsoft.EntityFrameworkCore;
using NxtHotspot.Application.Common;
using NxtHotspot.Domain.Entities;
using NxtHotspot.Infrastructure.Persistence;

namespace NxtHotspot.Infrastructure.Services;

public sealed class SmsSettingResolver : ISmsSettingResolver
{
    private readonly NxtDbContext _db;
    public SmsSettingResolver(NxtDbContext db) => _db = db;

    public async Task<OrganizationSmsSetting?> ResolveAsync(Guid organizationId, Guid? siteId, CancellationToken ct = default)
    {
        // 1. Site override. Matching OrganizationId too guarantees a site can never pull another tenant's credentials.
        if (siteId != null)
        {
            var siteSetting = await _db.OrganizationSmsSettings.AsNoTracking().FirstOrDefaultAsync(
                s => s.IsActive && s.SiteId == siteId && s.OrganizationId == organizationId, ct);
            if (siteSetting != null) return siteSetting;
        }

        // 2. Organization-level setting.
        var orgSetting = await _db.OrganizationSmsSettings.AsNoTracking().FirstOrDefaultAsync(
            s => s.IsActive && s.OrganizationId == organizationId && s.SiteId == null, ct);
        if (orgSetting != null) return orgSetting;

        // 3. Platform default configured by the Super Admin.
        return await _db.OrganizationSmsSettings.AsNoTracking()
            .Where(s => s.IsActive && s.OrganizationId == null && s.SiteId == null)
            .OrderBy(s => s.CreatedAt)
            .FirstOrDefaultAsync(ct);
    }
}
