using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using NxtHotspot.Application.Common;
using NxtHotspot.Application.Dtos;
using NxtHotspot.Domain.Entities;
using NxtHotspot.Domain.Enums;
using NxtHotspot.Infrastructure.Persistence;

namespace NxtHotspot.Infrastructure.Services;

/// <summary>Resolves which organization and sites a caller may see for RADIUS data.</summary>
internal static class RadiusScope
{
    public static async Task<(Guid? OrgId, IReadOnlyList<Guid>? Sites)> ResolveAsync(ICurrentUser current, ITenantAccess tenant, Guid? organizationId, CancellationToken ct)
    {
        if (current.IsPlatformAdmin) return (organizationId, null);
        var own = current.OrganizationId ?? throw new ForbiddenException();
        if (organizationId != null && organizationId != own) throw new ForbiddenException();
        return (own, await tenant.AccessibleSiteIdsAsync(own, ct));
    }

    public static IQueryable<RadiusSession> Apply(IQueryable<RadiusSession> q, Guid? orgId, IReadOnlyList<Guid>? sites)
    {
        if (orgId != null) q = q.Where(s => s.OrganizationId == orgId);
        if (sites != null) q = q.Where(s => sites.Contains(s.SiteId));
        return q;
    }
}

public sealed class SessionService : ISessionService
{
    private readonly NxtDbContext _db;
    private readonly ICurrentUser _current;
    private readonly ITenantAccess _tenant;

    public SessionService(NxtDbContext db, ICurrentUser current, ITenantAccess tenant) { _db = db; _current = current; _tenant = tenant; }

    private async Task<IQueryable<RadiusSession>> QueryAsync(SessionQuery f, CancellationToken ct)
    {
        var (org, sites) = await RadiusScope.ResolveAsync(_current, _tenant, f.OrganizationId, ct);
        var q = RadiusScope.Apply(_db.RadiusSessions.AsNoTracking(), org, sites);
        if (f.SiteId != null) q = q.Where(s => s.SiteId == f.SiteId);
        if (f.NasId != null) q = q.Where(s => s.NasId == f.NasId);
        if (f.SubscriberId != null) q = q.Where(s => s.SubscriberId == f.SubscriberId);
        if (f.Status != null) q = q.Where(s => s.Status == f.Status);
        if (f.From != null) { var from = f.From.Value.ToUniversalTime(); q = q.Where(s => s.StartedAt >= from); }
        if (f.To != null) { var to = f.To.Value.ToUniversalTime(); q = q.Where(s => s.StartedAt <= to); }
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var t = f.Search.Trim().ToLower();
            q = q.Where(s => s.Username.ToLower().Contains(t) || s.AcctSessionId.ToLower() == t || (s.CallingStationId != null && s.CallingStationId.ToLower().Contains(t))
                             || (s.FramedIpAddress != null && s.FramedIpAddress.Contains(t))
                             || (s.Subscriber != null && (s.Subscriber.FullName.ToLower().Contains(t) || s.Subscriber.NormalizedIdentifier.Contains(t.ToUpper())
                                                          || (s.Subscriber.RoomNumber != null && s.Subscriber.RoomNumber.ToLower() == t))));
        }
        return q;
    }

    private IQueryable<SessionDto> Project(IQueryable<RadiusSession> q) => q.Select(s => new SessionDto(
        s.Id, s.OrganizationId, _db.Organizations.Where(o => o.Id == s.OrganizationId).Select(o => o.Name).FirstOrDefault(), s.SiteId,
        _db.Sites.Where(x => x.Id == s.SiteId).Select(x => x.Name).FirstOrDefault() ?? "", s.NasId, s.Nas!.Name, s.SubscriberId,
        s.Subscriber != null ? s.Subscriber.FullName : null, s.Subscriber != null ? s.Subscriber.SubscriberType : null, s.Username, s.AcctSessionId,
        s.FramedIpAddress, s.CallingStationId, s.StartedAt, s.LastUpdateAt, s.StoppedAt, s.SessionTimeSeconds, s.InputBytes, s.OutputBytes,
        s.TerminateCause, s.Status,
        _db.DisconnectCommands.Where(c => c.SessionId == s.Id).OrderByDescending(c => c.CreatedAt).Select(c => (DisconnectStatus?)c.Status).FirstOrDefault()));

    public async Task<PagedResult<SessionDto>> ListAsync(SessionQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var size = Math.Clamp(query.PageSize, 1, 200);
        var q = await QueryAsync(query, ct);
        var total = await q.CountAsync(ct);
        var items = await Project(q.OrderByDescending(s => s.Status == RadiusSessionStatus.Active).ThenByDescending(s => s.StartedAt).Skip((page - 1) * size).Take(size)).ToListAsync(ct);
        return new PagedResult<SessionDto>(items, page, size, total);
    }

    public async Task<DisconnectCommandDto> DisconnectAsync(Guid sessionId, CancellationToken ct = default)
    {
        var session = await _db.RadiusSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct) ?? throw new NotFoundException("Session not found.");
        var (org, sites) = await RadiusScope.ResolveAsync(_current, _tenant, null, ct);
        if ((org != null && session.OrganizationId != org) || (sites != null && !sites.Contains(session.SiteId))) throw new NotFoundException("Session not found.");
        if (session.Status != RadiusSessionStatus.Active) throw new ConflictException("The session is not active.");
        var pending = await _db.DisconnectCommands.FirstOrDefaultAsync(c => c.SessionId == sessionId && c.Status == DisconnectStatus.Pending, ct);
        if (pending != null) return Map(pending);
        var cmd = new DisconnectCommand { OrganizationId = session.OrganizationId, SiteId = session.SiteId, NasId = session.NasId, SessionId = session.Id, RequestedByAdminUserId = _current.AdminUserId };
        _db.DisconnectCommands.Add(cmd);
        _db.AuditLogs.Add(Audit.Entry(session.OrganizationId, _current.AdminUserId, "SessionDisconnectRequested", nameof(RadiusSession), session.Id.ToString(),
            $"User={session.Username}, Mac={session.CallingStationId}"));
        await _db.SaveChangesAsync(ct);
        return Map(cmd);
    }

    public async Task<List<DisconnectCommandDto>> RecentDisconnectsAsync(Guid? organizationId, CancellationToken ct = default)
    {
        var (org, sites) = await RadiusScope.ResolveAsync(_current, _tenant, organizationId, ct);
        var q = _db.DisconnectCommands.AsNoTracking().AsQueryable();
        if (org != null) q = q.Where(c => c.OrganizationId == org);
        if (sites != null) q = q.Where(c => sites.Contains(c.SiteId));
        return await q.OrderByDescending(c => c.CreatedAt).Take(50)
            .Select(c => new DisconnectCommandDto(c.Id, c.SessionId, c.NasId, c.Status, c.Attempts, c.Result, c.CreatedAt, c.CompletedAt)).ToListAsync(ct);
    }

    public async Task<FileResultDto> ExportAsync(SessionQuery query, CancellationToken ct = default)
    {
        var rows = await Project((await QueryAsync(query, ct)).OrderByDescending(s => s.StartedAt).Take(50_000)).ToListAsync(ct);
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Accounting");
        string[] h = { "Started (UTC)", "Stopped (UTC)", "Status", "Organization", "Site", "NAS", "Username", "Subscriber", "Type", "MAC", "IP", "Session time (s)", "Download (bytes)", "Upload (bytes)", "Terminate cause", "Acct-Session-Id" };
        for (var i = 0; i < h.Length; i++) ws.Cell(1, i + 1).Value = h[i];
        ws.Row(1).Style.Font.Bold = true;
        var r = 2;
        foreach (var s in rows)
        {
            ws.Cell(r, 1).Value = s.StartedAt; ws.Cell(r, 1).Style.DateFormat.Format = "yyyy-mm-dd hh:mm:ss";
            if (s.StoppedAt is { } st) { ws.Cell(r, 2).Value = st; ws.Cell(r, 2).Style.DateFormat.Format = "yyyy-mm-dd hh:mm:ss"; }
            ws.Cell(r, 3).Value = s.Status.ToString(); ws.Cell(r, 4).Value = s.OrganizationName ?? ""; ws.Cell(r, 5).Value = s.SiteName; ws.Cell(r, 6).Value = s.NasName;
            ws.Cell(r, 7).Value = s.Username; ws.Cell(r, 8).Value = s.SubscriberName ?? ""; ws.Cell(r, 9).Value = s.SubscriberType?.ToString() ?? "";
            ws.Cell(r, 10).Value = s.MacAddress ?? ""; ws.Cell(r, 11).Value = s.FramedIpAddress ?? ""; ws.Cell(r, 12).Value = s.SessionTimeSeconds;
            // Downloaded by the client = Acct-Output-Octets (from the NAS's perspective).
            ws.Cell(r, 13).Value = s.OutputBytes; ws.Cell(r, 14).Value = s.InputBytes; ws.Cell(r, 15).Value = s.TerminateCause ?? ""; ws.Cell(r, 16).Value = s.AcctSessionId;
            r++;
        }
        ws.Columns().AdjustToContents(1, Math.Min(r, 200));
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return new FileResultDto($"nxt-accounting-{DateTime.UtcNow:yyyyMMdd-HHmm}.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", ms.ToArray());
    }

    private static DisconnectCommandDto Map(DisconnectCommand c) => new(c.Id, c.SessionId, c.NasId, c.Status, c.Attempts, c.Result, c.CreatedAt, c.CompletedAt);
}

public sealed class UsageReportService : IUsageReportService
{
    private readonly NxtDbContext _db;
    private readonly ICurrentUser _current;
    private readonly ITenantAccess _tenant;

    public UsageReportService(NxtDbContext db, ICurrentUser current, ITenantAccess tenant) { _db = db; _current = current; _tenant = tenant; }

    public async Task<UsageReportDto> GetAsync(Guid? organizationId, Guid? siteId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        if (toUtc <= fromUtc) throw new ValidationFailedException("The end date must be after the start date.");
        if ((toUtc - fromUtc).TotalDays > 366) throw new ValidationFailedException("Reports cover at most one year.");
        var (org, sites) = await RadiusScope.ResolveAsync(_current, _tenant, organizationId, ct);
        var q = RadiusScope.Apply(_db.RadiusSessions.AsNoTracking(), org, sites).Where(s => s.StartedAt >= fromUtc && s.StartedAt < toUtc);
        if (siteId != null) q = q.Where(s => s.SiteId == siteId);

        // Usage is attributed to the day the session started (UTC).
        var rows = await q.Select(s => new { s.StartedAt, s.SubscriberId, s.Username, s.SiteId, s.NasId, s.InputBytes, s.OutputBytes, s.SessionTimeSeconds,
            Type = s.Subscriber != null ? (SubscriberType?)s.Subscriber.SubscriberType : null, Name = s.Subscriber != null ? s.Subscriber.FullName : null }).ToListAsync(ct);

        var days = Enumerable.Range(0, Math.Max(1, (int)Math.Ceiling((toUtc.Date - fromUtc.Date).TotalDays) + 1)).Select(i => fromUtc.Date.AddDays(i)).Where(d => d < toUtc).ToList();
        var daily = days.Select(d =>
        {
            var g = rows.Where(r => r.StartedAt.Date == d).ToList();
            return new UsagePointDto(d.ToString("yyyy-MM-dd"), g.Count, g.Select(r => r.SubscriberId?.ToString() ?? r.Username).Distinct().Count(),
                g.Sum(r => r.InputBytes), g.Sum(r => r.OutputBytes), g.Sum(r => r.SessionTimeSeconds));
        }).ToList();

        var siteNames = await _db.Sites.AsNoTracking().Where(s => rows.Select(r => r.SiteId).Distinct().Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        var nasNames = await _db.NasDevices.AsNoTracking().Where(n => rows.Select(r => r.NasId).Distinct().Contains(n.Id)).ToDictionaryAsync(n => n.Id, n => n.Name, ct);

        var topSubs = rows.GroupBy(r => r.SubscriberId?.ToString() ?? r.Username)
            .Select(g => new UsageRowDto(g.Key, g.First().Name ?? g.First().Username, g.Count(), g.Sum(r => r.InputBytes + r.OutputBytes), g.Sum(r => r.SessionTimeSeconds)))
            .OrderByDescending(x => x.Bytes).Take(10).ToList();
        var bySite = rows.GroupBy(r => r.SiteId).Select(g => new UsageRowDto(g.Key.ToString(), siteNames.GetValueOrDefault(g.Key, "Site"), g.Count(), g.Sum(r => r.InputBytes + r.OutputBytes), g.Sum(r => r.SessionTimeSeconds))).OrderByDescending(x => x.Bytes).ToList();
        var byNas = rows.GroupBy(r => r.NasId).Select(g => new UsageRowDto(g.Key.ToString(), nasNames.GetValueOrDefault(g.Key, "NAS"), g.Count(), g.Sum(r => r.InputBytes + r.OutputBytes), g.Sum(r => r.SessionTimeSeconds))).OrderByDescending(x => x.Bytes).ToList();
        var byType = rows.GroupBy(r => r.Type?.ToString() ?? "Unknown").Select(g => new UsageRowDto(g.Key, g.Key, g.Count(), g.Sum(r => r.InputBytes + r.OutputBytes), g.Sum(r => r.SessionTimeSeconds))).OrderByDescending(x => x.Bytes).ToList();

        return new UsageReportDto(fromUtc, toUtc, rows.Count, rows.Select(r => r.SubscriberId?.ToString() ?? r.Username).Distinct().Count(),
            rows.Sum(r => r.InputBytes), rows.Sum(r => r.OutputBytes), rows.Sum(r => r.SessionTimeSeconds), daily, topSubs, bySite, byNas, byType);
    }
}

public sealed class RadiusMonitorService : IRadiusMonitorService
{
    public static readonly TimeSpan OnlineWindow = TimeSpan.FromSeconds(60);
    private readonly NxtDbContext _db;
    private readonly ICurrentUser _current;
    private readonly ITenantAccess _tenant;

    public RadiusMonitorService(NxtDbContext db, ICurrentUser current, ITenantAccess tenant) { _db = db; _current = current; _tenant = tenant; }

    public async Task<RadiusOverviewDto> OverviewAsync(Guid? organizationId, CancellationToken ct = default)
    {
        var (org, sites) = await RadiusScope.ResolveAsync(_current, _tenant, organizationId, ct);
        var now = DateTime.UtcNow;
        var servers = (await _db.RadiusServerStatuses.AsNoTracking().OrderBy(s => s.InstanceId).ToListAsync(ct))
            .Select(s => new RadiusServerStatusDto(s.InstanceId, s.Version, s.HostName, s.AuthPort, s.AcctPort, s.StartedAt, s.LastHeartbeatAt,
                now - s.LastHeartbeatAt < OnlineWindow, s.AuthRequests, s.AuthAccepts, s.AuthRejects, s.AcctRequests, s.Dropped)).ToList();

        var logs = _db.RadiusRequestLogs.AsNoTracking().Where(l => l.CreatedAt >= now.AddHours(-24));
        if (org != null) logs = logs.Where(l => l.OrganizationId == org);
        if (sites != null) logs = logs.Where(l => l.SiteId != null && sites.Contains(l.SiteId.Value));
        var counts = await logs.GroupBy(l => l.Result).Select(g => new { g.Key, C = g.Count() }).ToListAsync(ct);
        var reasons = await logs.Where(l => l.Result == RadiusResult.Reject || l.Result == RadiusResult.Dropped).GroupBy(l => l.Reason ?? "")
            .Select(g => new { g.Key, C = g.Count() }).OrderByDescending(x => x.C).Take(6).ToListAsync(ct);

        var nas = _db.NasDevices.AsNoTracking().AsQueryable();
        if (org != null) nas = nas.Where(n => n.OrganizationId == org);
        if (sites != null) nas = nas.Where(n => sites.Contains(n.SiteId));
        var nasList = await nas.OrderByDescending(n => n.LastSeenAt).Take(50)
            .Select(n => new NasHealthDto(n.Id, n.Name, n.IpAddress, _db.Organizations.Where(o => o.Id == n.OrganizationId).Select(o => o.Name).FirstOrDefault() ?? "",
                n.Site!.Name, n.IsActive, n.LastSeenAt, _db.RadiusSessions.Count(s => s.NasId == n.Id && s.Status == RadiusSessionStatus.Active)))
            .ToListAsync(ct);

        var active = await RadiusScope.Apply(_db.RadiusSessions.AsNoTracking(), org, sites).CountAsync(s => s.Status == RadiusSessionStatus.Active, ct);
        int C(RadiusResult r) => counts.FirstOrDefault(x => x.Key == r)?.C ?? 0;
        return new RadiusOverviewDto(servers.Any(s => s.IsOnline), _current.IsPlatformAdmin ? servers : servers.Select(s => s with { HostName = null }).ToList(),
            active, counts.Sum(x => x.C), C(RadiusResult.Accept), C(RadiusResult.Reject), C(RadiusResult.Dropped),
            reasons.Select(r => new LabelCount(r.Key, r.C)).ToList(), nasList);
    }

    public async Task<PagedResult<RadiusRequestLogDto>> RequestLogsAsync(Guid? organizationId, RadiusResult? result, string? search, DateTime? fromUtc,
        int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var (org, sites) = await RadiusScope.ResolveAsync(_current, _tenant, organizationId, ct);
        var q = _db.RadiusRequestLogs.AsNoTracking().AsQueryable();
        if (org != null) q = q.Where(l => l.OrganizationId == org);
        if (sites != null) q = q.Where(l => l.SiteId != null && sites.Contains(l.SiteId.Value));
        if (result != null) q = q.Where(l => l.Result == result);
        if (fromUtc != null) q = q.Where(l => l.CreatedAt >= fromUtc);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var t = search.Trim().ToLower();
            q = q.Where(l => (l.Username != null && l.Username.ToLower().Contains(t)) || (l.MacAddress != null && l.MacAddress.ToLower().Contains(t))
                             || l.SourceIp.Contains(t) || (l.Reason != null && l.Reason.ToLower().Contains(t)));
        }
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(l => l.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(l => new RadiusRequestLogDto(l.Id, l.OrganizationId, _db.Organizations.Where(o => o.Id == l.OrganizationId).Select(o => o.Name).FirstOrDefault(),
                l.NasId, _db.NasDevices.Where(n => n.Id == l.NasId).Select(n => n.Name).FirstOrDefault(), l.SourceIp, l.PacketKind, l.Result, l.Username,
                l.MacAddress, l.Reason, l.CreatedAt))
            .ToListAsync(ct);
        return new PagedResult<RadiusRequestLogDto>(items, page, pageSize, total);
    }
}
