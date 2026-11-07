using Microsoft.EntityFrameworkCore;
using NxtHotspot.Application.Common;
using NxtHotspot.Domain.Entities;
using NxtHotspot.Domain.Enums;
using NxtHotspot.Infrastructure.Persistence;

namespace NxtHotspot.Infrastructure.Services;

/// <summary>Writes DisconnectCommands; the RADIUS server picks them up and sends RFC 5176 Disconnect-Requests.</summary>
public sealed class DisconnectQueue : IDisconnectQueue
{
    private readonly NxtDbContext _db;
    private readonly ICurrentUser _current;

    public DisconnectQueue(NxtDbContext db, ICurrentUser current) { _db = db; _current = current; }

    public async Task<int> QueueForSubscriberAsync(Guid subscriberId, string reason, CancellationToken ct = default)
    {
        var sessions = await _db.RadiusSessions.Where(s => s.SubscriberId == subscriberId && s.Status == RadiusSessionStatus.Active).ToListAsync(ct);
        var already = await _db.DisconnectCommands.Where(c => sessions.Select(s => s.Id).Contains(c.SessionId) && c.Status == DisconnectStatus.Pending)
            .Select(c => c.SessionId).ToListAsync(ct);
        var count = 0;
        foreach (var s in sessions.Where(s => !already.Contains(s.Id)))
        {
            _db.DisconnectCommands.Add(new DisconnectCommand
            {
                OrganizationId = s.OrganizationId, SiteId = s.SiteId, NasId = s.NasId, SessionId = s.Id,
                RequestedByAdminUserId = _current.AdminUserId, Result = reason,
            });
            count++;
        }
        if (count > 0) await _db.SaveChangesAsync(ct);
        return count;
    }
}
