using Microsoft.EntityFrameworkCore;
using NxtHotspot.Application.Dtos;
using NxtHotspot.Domain.Enums;
using NxtHotspot.Infrastructure.Persistence;

namespace NxtHotspot.Infrastructure.Services;

/// <summary>Hotspot counts (NAS, subscribers, packages, sessions) for dashboards, straight from the database.</summary>
internal static class HotspotStatistics
{
    public readonly record struct SiteCounts(int Nas, int Subscribers, int ActiveSessions);

    public static async Task<HotspotSummaryDto> SummaryAsync(NxtDbContext db, Guid? organizationId, IReadOnlyList<Guid>? siteIds, CancellationToken ct)
    {
        var nas = db.NasDevices.AsNoTracking().AsQueryable();
        var subs = db.Subscribers.AsNoTracking().AsQueryable();
        var pkgs = db.InternetPackages.AsNoTracking().AsQueryable();
        var sessions = db.RadiusSessions.AsNoTracking().AsQueryable();
        if (organizationId != null)
        {
            nas = nas.Where(x => x.OrganizationId == organizationId);
            subs = subs.Where(x => x.OrganizationId == organizationId);
            pkgs = pkgs.Where(x => x.OrganizationId == organizationId);
            sessions = sessions.Where(x => x.OrganizationId == organizationId);
        }
        if (siteIds != null)
        {
            nas = nas.Where(x => siteIds.Contains(x.SiteId));
            subs = subs.Where(x => x.SiteId == null || siteIds.Contains(x.SiteId.Value));
            pkgs = pkgs.Where(x => x.SiteId == null || siteIds.Contains(x.SiteId.Value));
            sessions = sessions.Where(x => siteIds.Contains(x.SiteId));
        }
        var since = DateTime.UtcNow.AddDays(-30);
        var recent = sessions.Where(s => s.StartedAt >= since);
        var bytes = await recent.Select(s => s.InputBytes + s.OutputBytes).ToListAsync(ct);
        return new HotspotSummaryDto(
            await nas.CountAsync(ct), await nas.CountAsync(n => n.IsActive, ct),
            await subs.CountAsync(ct), await subs.CountAsync(s => s.Status == SubscriberStatus.Active, ct),
            await pkgs.CountAsync(p => p.IsActive, ct),
            await sessions.CountAsync(s => s.Status == RadiusSessionStatus.Active, ct),
            bytes.Sum(), bytes.Count);
    }

    public static async Task<Dictionary<Guid, SiteCounts>> PerSiteAsync(NxtDbContext db, Guid organizationId, CancellationToken ct)
    {
        var nas = await db.NasDevices.AsNoTracking().Where(n => n.OrganizationId == organizationId).GroupBy(n => n.SiteId)
            .Select(g => new { g.Key, C = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.C, ct);
        var subs = await db.Subscribers.AsNoTracking().Where(s => s.OrganizationId == organizationId && s.SiteId != null).GroupBy(s => s.SiteId!.Value)
            .Select(g => new { g.Key, C = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.C, ct);
        var sess = await db.RadiusSessions.AsNoTracking().Where(s => s.OrganizationId == organizationId && s.Status == RadiusSessionStatus.Active)
            .GroupBy(s => s.SiteId).Select(g => new { g.Key, C = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.C, ct);
        return nas.Keys.Union(subs.Keys).Union(sess.Keys).Distinct()
            .ToDictionary(k => k, k => new SiteCounts(nas.GetValueOrDefault(k), subs.GetValueOrDefault(k), sess.GetValueOrDefault(k)));
    }
}
