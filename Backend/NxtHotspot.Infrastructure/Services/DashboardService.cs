using Microsoft.EntityFrameworkCore;
using NxtHotspot.Application.Common;
using NxtHotspot.Application.Dtos;
using NxtHotspot.Domain.Enums;
using NxtHotspot.Infrastructure.Persistence;

namespace NxtHotspot.Infrastructure.Services;

public sealed class DashboardService : IDashboardService
{
    /// <summary>Wallets below this balance are reported as low.</summary>
    public const decimal LowBalanceThreshold = 100m;

    private readonly NxtDbContext _db;
    private readonly ICurrentUser _current;
    private readonly ITenantAccess _tenant;
    private readonly IAuditLogService _audit;

    public DashboardService(NxtDbContext db, ICurrentUser current, ITenantAccess tenant, IAuditLogService audit)
    {
        _db = db; _current = current; _tenant = tenant; _audit = audit;
    }

    public async Task<PlatformDashboardDto> GetPlatformAsync(CancellationToken ct = default)
    {
        if (!_current.IsPlatformAdmin) throw new ForbiddenException();

        var orgs = await _db.Organizations.AsNoTracking()
            .Select(o => new { o.Status, Type = o.OrganizationType!.TypeName, Plan = o.SubscriptionPlan != null ? o.SubscriptionPlan.Name : null, o.CreatedAt })
            .ToListAsync(ct);

        var byStatus = Enum.GetValues<OrganizationStatus>()
            .Select(s => new LabelCount(s.ToString(), orgs.Count(o => o.Status == s))).ToList();
        var byType = orgs.GroupBy(o => o.Type).Select(g => new LabelCount(g.Key, g.Count()))
            .OrderByDescending(x => x.Count).ThenBy(x => x.Label).ToList();
        var byPlan = orgs.GroupBy(o => o.Plan ?? "No plan").Select(g => new LabelCount(g.Key, g.Count()))
            .OrderByDescending(x => x.Count).ToList();

        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var months = Enumerable.Range(0, 6).Select(i => monthStart.AddMonths(i - 5)).ToList();
        var registrations = months.Select(m => new MonthCount(m.ToString("yyyy-MM"),
            orgs.Count(o => o.CreatedAt >= m && o.CreatedAt < m.AddMonths(1)))).ToList();

        var sites = new CountPair(await _db.Sites.CountAsync(ct), await _db.Sites.CountAsync(s => s.IsActive, ct));
        var admins = new CountPair(await _db.AdminUsers.CountAsync(ct), await _db.AdminUsers.CountAsync(u => u.IsActive, ct));
        var plans = new CountPair(await _db.SubscriptionPlans.CountAsync(ct), await _db.SubscriptionPlans.CountAsync(p => p.IsActive, ct));

        var recentOrgs = await _db.Organizations.AsNoTracking().OrderByDescending(o => o.CreatedAt).Take(5)
            .Select(o => new OrganizationSummaryDto(o.Id, o.Name, o.OrganizationType!.TypeName, o.Status, o.Sites.Count, o.CreatedAt))
            .ToListAsync(ct);
        var activity = await _audit.ListAsync(null, null, null, null, 1, 8, ct);

        return new PlatformDashboardDto(orgs.Count, byStatus, byType, sites, admins, plans, byPlan,
            await WalletSummaryAsync(null, null, ct), await HotspotStatistics.SummaryAsync(_db, null, null, ct),
            registrations, recentOrgs, activity.Items);
    }

    public async Task<OrganizationStatisticsDto> GetOrganizationAsync(Guid organizationId, CancellationToken ct = default)
    {
        var siteIds = await _tenant.AccessibleSiteIdsAsync(organizationId, ct); // also enforces tenant access
        var org = await _db.Organizations.AsNoTracking()
            .Where(o => o.Id == organizationId)
            .Select(o => new
            {
                o.Id, o.Name, Type = o.OrganizationType!.TypeName, o.Status,
                Plan = o.SubscriptionPlan
            })
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException("Organization not found.");

        var sitesQ = _db.Sites.AsNoTracking().Where(s => s.OrganizationId == organizationId);
        if (siteIds != null) sitesQ = sitesQ.Where(s => siteIds.Contains(s.Id));
        var sites = await sitesQ.OrderByDescending(s => s.IsPrimary).ThenBy(s => s.Name)
            .Select(s => new { s.Id, s.Name, s.Code, s.IsActive }).ToListAsync(ct);

        var balances = await _db.SiteWallets.AsNoTracking().Where(w => w.OrganizationId == organizationId)
            .ToDictionaryAsync(w => w.SiteId, w => w.Balance, ct);
        var perSite = await HotspotStatistics.PerSiteAsync(_db, organizationId, ct);

        var breakdown = sites.Select(s =>
        {
            perSite.TryGetValue(s.Id, out var h);
            return new SiteStatisticsDto(s.Id, s.Name, s.Code, s.IsActive, balances.GetValueOrDefault(s.Id),
                h.Nas, h.Subscribers, h.ActiveSessions);
        }).ToList();

        var admins = _db.AdminUsers.AsNoTracking().Where(u => u.OrganizationId == organizationId);
        var adminCounts = new CountPair(await admins.CountAsync(ct), await admins.CountAsync(u => u.IsActive, ct));

        var canSeeAudit = _current.Permissions.Contains(Permissions.AuditRead) && siteIds == null;
        var activity = canSeeAudit ? (await _audit.ListAsync(organizationId, null, null, null, 1, 8, ct)).Items : Array.Empty<AuditLogDto>();

        return new OrganizationStatisticsDto(org.Id, org.Name, org.Type, org.Status, org.Plan?.Name, org.Plan?.MaxSites,
            org.Plan?.MaxNasDevices, org.Plan?.MaxUsers,
            new CountPair(sites.Count, sites.Count(s => s.IsActive)), adminCounts,
            await WalletSummaryAsync(organizationId, siteIds, ct),
            await HotspotStatistics.SummaryAsync(_db, organizationId, siteIds, ct),
            breakdown, activity);
    }

    private async Task<WalletSummaryDto> WalletSummaryAsync(Guid? organizationId, IReadOnlyList<Guid>? siteIds, CancellationToken ct)
    {
        var wallets = _db.SiteWallets.AsNoTracking().AsQueryable();
        var tx = _db.SiteWalletTransactions.AsNoTracking().AsQueryable();
        if (organizationId != null)
        {
            wallets = wallets.Where(w => w.OrganizationId == organizationId);
            tx = tx.Where(t => t.OrganizationId == organizationId);
        }
        if (siteIds != null)
        {
            wallets = wallets.Where(w => siteIds.Contains(w.SiteId));
            tx = tx.Where(t => siteIds.Contains(t.SiteId));
        }
        var since = DateTime.UtcNow.AddDays(-30);
        var recent = tx.Where(t => t.CreatedAt >= since);
        var balances = await wallets.Select(w => w.Balance).ToListAsync(ct);
        var credits = await recent.Where(t => t.Type == WalletTransactionType.Credit).Select(t => t.Amount).ToListAsync(ct);
        var debits = await recent.Where(t => t.Type == WalletTransactionType.Debit).Select(t => t.Amount).ToListAsync(ct);
        return new WalletSummaryDto(balances.Sum(), "INR", balances.Count, credits.Sum(), debits.Sum(),
            balances.Count(b => b < LowBalanceThreshold));
    }
}
