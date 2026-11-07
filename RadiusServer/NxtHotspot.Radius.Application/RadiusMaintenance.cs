using Microsoft.EntityFrameworkCore;
using NxtHotspot.Domain.Entities;
using NxtHotspot.Domain.Enums;
using NxtHotspot.Infrastructure.Persistence;

namespace NxtHotspot.Radius.Application;

/// <summary>Periodic housekeeping: stale sessions, expired one-time credentials and OTPs, heartbeat.</summary>
public sealed class RadiusMaintenance
{
    private readonly NxtDbContext _db;
    public RadiusMaintenance(NxtDbContext db) => _db = db;

    public async Task<int> MarkStaleSessionsAsync(TimeSpan staleAfter, CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow - staleAfter;
        var stale = await _db.RadiusSessions.Where(s => s.Status == RadiusSessionStatus.Active && s.LastUpdateAt < cutoff).ToListAsync(ct);
        foreach (var s in stale) s.Status = RadiusSessionStatus.Stale;
        var oldCredentials = await _db.RadiusCredentials.Where(c => c.ExpiresAt < DateTime.UtcNow.AddDays(-1)).ToListAsync(ct);
        _db.RadiusCredentials.RemoveRange(oldCredentials);
        var oldOtps = await _db.OtpChallenges.Where(o => o.ExpiresAt < DateTime.UtcNow.AddDays(-1)).ToListAsync(ct);
        _db.OtpChallenges.RemoveRange(oldOtps);
        await _db.SaveChangesAsync(ct);
        return stale.Count;
    }

    public async Task HeartbeatAsync(string instanceId, string version, int authPort, int acctPort, DateTime startedAt, RadiusCounters c, CancellationToken ct)
    {
        var row = await _db.RadiusServerStatuses.FirstOrDefaultAsync(x => x.InstanceId == instanceId, ct);
        if (row == null)
        {
            row = new RadiusServerStatus { InstanceId = instanceId };
            _db.RadiusServerStatuses.Add(row);
        }
        row.Version = version;
        row.HostName = Environment.MachineName;
        row.AuthPort = authPort;
        row.AcctPort = acctPort;
        row.StartedAt = startedAt;
        row.LastHeartbeatAt = DateTime.UtcNow;
        row.AuthRequests = Interlocked.Read(ref c.AuthRequests);
        row.AuthAccepts = Interlocked.Read(ref c.AuthAccepts);
        row.AuthRejects = Interlocked.Read(ref c.AuthRejects);
        row.AcctRequests = Interlocked.Read(ref c.AcctRequests);
        row.Dropped = Interlocked.Read(ref c.Dropped);
        await _db.SaveChangesAsync(ct);
    }
}
