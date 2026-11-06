using Microsoft.EntityFrameworkCore;
using NxtHotspot.Application.Common;
using NxtHotspot.Application.Dtos;
using NxtHotspot.Infrastructure.Persistence;

namespace NxtHotspot.Infrastructure.Services;

public sealed class AuditLogService : IAuditLogService
{
    private readonly NxtDbContext _db;
    private readonly ICurrentUser _current;

    public AuditLogService(NxtDbContext db, ICurrentUser current) { _db = db; _current = current; }

    public async Task<PagedResult<AuditLogDto>> ListAsync(Guid? organizationId, string? action, DateTime? fromUtc, DateTime? toUtc,
        int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var q = _db.AuditLogs.AsNoTracking().AsQueryable();
        if (_current.IsPlatformAdmin)
        {
            if (organizationId != null) q = q.Where(a => a.OrganizationId == organizationId);
        }
        else
        {
            var own = _current.OrganizationId ?? throw new ForbiddenException();
            if (organizationId != null && organizationId != own) throw new ForbiddenException();
            // The audit trail spans every site, so site-restricted admins do not get it.
            if (!_current.HasAllSites) throw new ForbiddenException("Only administrators with access to all sites can view the audit log.");
            q = q.Where(a => a.OrganizationId == own);
        }

        if (!string.IsNullOrWhiteSpace(action)) { var a = action.Trim(); q = q.Where(x => x.Action == a); }
        if (fromUtc != null) q = q.Where(x => x.CreatedAt >= fromUtc);
        if (toUtc != null) q = q.Where(x => x.CreatedAt <= toUtc);

        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        var items = rows.Select(x => new AuditLogDto(x.Id, x.OrganizationId, x.AdminUserId, x.Action, x.EntityType, x.EntityId,
            x.Details, x.IpAddress, x.CreatedAt)).ToList();
        return new PagedResult<AuditLogDto>(items, page, pageSize, total);
    }
}
