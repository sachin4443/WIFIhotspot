using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NxtHotspot.Domain.Entities;
using NxtHotspot.Domain.Enums;
using NxtHotspot.Infrastructure.Persistence;
using NxtHotspot.RadiusProtocol;

namespace NxtHotspot.Radius.Application;

/// <summary>
/// Accounting-Request pipeline (RFC 2866): verify authenticator → idempotency check → Start/Interim/Stop update the session;
/// Accounting-On/Off close the NAS's open sessions. Gigaword counters are combined into 64-bit totals.
/// </summary>
public sealed class RadiusAccountingService
{
    private readonly NxtDbContext _db;
    private readonly NasDirectory _nas;
    private readonly RadiusCounters _counters;
    private readonly ILogger<RadiusAccountingService> _logger;

    public RadiusAccountingService(NxtDbContext db, NasDirectory nas, RadiusCounters counters, ILogger<RadiusAccountingService> logger)
    {
        _db = db; _nas = nas; _counters = counters; _logger = logger;
    }

    public async Task<byte[]?> HandleAsync(byte[] datagram, IPEndPoint remote, CancellationToken ct)
    {
        Interlocked.Increment(ref _counters.AcctRequests);
        var nas = await _nas.FindAsync(_db, remote.Address, ct);
        RadiusPacket request;
        try { request = RadiusPacket.Parse(datagram); }
        catch (RadiusPacketException ex) { return await DropAsync(nas, remote, null, $"Malformed packet: {ex.Message}", ct); }
        var username = request.GetString(RadiusAttributeType.UserName);
        if (request.Code != RadiusCode.AccountingRequest) return await DropAsync(nas, remote, username, $"Unexpected code {request.Code} on accounting port", ct);
        if (nas == null || !nas.IsActive) return await DropAsync(nas, remote, username, nas == null ? "Unknown NAS (source IP not registered)" : "NAS is inactive", ct);
        if (!RadiusCrypto.VerifyZeroBasedRequest(request, nas.Secret)) return await DropAsync(nas, remote, username, "Invalid accounting authenticator (check the shared secret)", ct);
        var ma = RadiusCrypto.VerifyMessageAuthenticator(request, nas.Secret);
        if (ma == false) return await DropAsync(nas, remote, username, "Invalid Message-Authenticator", ct);

        var status = (int)(request.GetInteger(RadiusAttributeType.AcctStatusType) ?? 0);
        var now = DateTime.UtcNow;
        var nasEntity = await _db.NasDevices.FirstOrDefaultAsync(n => n.Id == nas.Id, ct);
        if (nasEntity != null) nasEntity.LastSeenAt = now;

        if (status is AcctStatusType.AccountingOn or AcctStatusType.AccountingOff)
        {
            var open = await _db.RadiusSessions.Where(s => s.NasId == nas.Id && s.Status == RadiusSessionStatus.Active).ToListAsync(ct);
            foreach (var s in open) { s.Status = RadiusSessionStatus.Stopped; s.StoppedAt = now; s.TerminateCause = "NAS-Reboot"; }
            _db.RadiusRequestLogs.Add(RadiusLog.Entry(nas, remote.Address, RadiusPacketKind.AccountingRequest, RadiusResult.Accounted, username, null,
                $"Accounting-{(status == AcctStatusType.AccountingOn ? "On" : "Off")}: closed {open.Count} sessions"));
            await _db.SaveChangesAsync(ct);
            return Respond(request, nas, ma == true);
        }

        var sessionId = request.GetString(RadiusAttributeType.AcctSessionId);
        if (status is not (AcctStatusType.Start or AcctStatusType.Stop or AcctStatusType.InterimUpdate) || string.IsNullOrEmpty(sessionId))
        {
            _db.RadiusRequestLogs.Add(RadiusLog.Entry(nas, remote.Address, RadiusPacketKind.AccountingRequest, RadiusResult.Accounted, username, null, $"Ignored status {status}"));
            await _db.SaveChangesAsync(ct);
            return Respond(request, nas, ma == true);
        }

        long sessionTime = request.GetInteger(RadiusAttributeType.AcctSessionTime) ?? 0;
        long delay = request.GetInteger(RadiusAttributeType.AcctDelayTime) ?? 0;
        long input = ((long)(request.GetInteger(RadiusAttributeType.AcctInputGigawords) ?? 0) << 32) + (request.GetInteger(RadiusAttributeType.AcctInputOctets) ?? 0);
        long output = ((long)(request.GetInteger(RadiusAttributeType.AcctOutputGigawords) ?? 0) << 32) + (request.GetInteger(RadiusAttributeType.AcctOutputOctets) ?? 0);

        // Idempotency: a retransmitted packet is acknowledged but not applied twice.
        var duplicate = await _db.RadiusAccountingEvents.AnyAsync(e => e.NasId == nas.Id && e.AcctSessionId == sessionId && e.StatusType == status
                                                                       && e.SessionTime == sessionTime && e.DelayTime == delay, ct);
        if (duplicate) return Respond(request, nas, ma == true);
        _db.RadiusAccountingEvents.Add(new RadiusAccountingEvent { NasId = nas.Id, AcctSessionId = sessionId, StatusType = status, SessionTime = sessionTime, DelayTime = delay, InputBytes = input, OutputBytes = output });

        var eventTime = now.AddSeconds(-delay);
        var session = await _db.RadiusSessions.FirstOrDefaultAsync(s => s.NasId == nas.Id && s.AcctSessionId == sessionId, ct);
        if (session == null)
        {
            session = new RadiusSession
            {
                OrganizationId = nas.OrganizationId, SiteId = nas.SiteId, NasId = nas.Id, AcctSessionId = sessionId,
                Username = username ?? "", StartedAt = eventTime.AddSeconds(-sessionTime), SubscriberId = await ResolveSubscriberAsync(request, nas, username, ct),
            };
            _db.RadiusSessions.Add(session);
        }
        session.LastUpdateAt = eventTime;
        session.FramedIpAddress = request.Get(RadiusAttributeType.FramedIpAddress) is { Value.Length: 4 } ip ? new IPAddress(ip.Value).ToString() : session.FramedIpAddress;
        session.CallingStationId = RadiusLog.NormalizeMac(request.GetString(RadiusAttributeType.CallingStationId)) ?? request.GetString(RadiusAttributeType.CallingStationId) ?? session.CallingStationId;
        session.CalledStationId = request.GetString(RadiusAttributeType.CalledStationId) ?? session.CalledStationId;
        session.NasPortId = request.GetString(RadiusAttributeType.NasPortId) ?? session.NasPortId;
        // Counters are cumulative per session; never move backwards on a reordered packet.
        session.SessionTimeSeconds = Math.Max(session.SessionTimeSeconds, sessionTime);
        session.InputBytes = Math.Max(session.InputBytes, input);
        session.OutputBytes = Math.Max(session.OutputBytes, output);
        if (status == AcctStatusType.Stop)
        {
            session.Status = RadiusSessionStatus.Stopped;
            session.StoppedAt = eventTime;
            session.TerminateCause = TerminateCause(request.GetInteger(RadiusAttributeType.AcctTerminateCause));
        }
        else if (session.Status != RadiusSessionStatus.Stopped)
        {
            session.Status = RadiusSessionStatus.Active;
        }

        _db.RadiusRequestLogs.Add(RadiusLog.Entry(nas, remote.Address, RadiusPacketKind.AccountingRequest, RadiusResult.Accounted, username, session.CallingStationId,
            status switch { AcctStatusType.Start => "Start", AcctStatusType.Stop => $"Stop ({session.TerminateCause})", _ => "Interim-Update" }));
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex)
        {
            // A concurrent duplicate hit the unique index: it was already applied.
            _logger.LogDebug(ex, "Duplicate accounting packet ignored");
        }
        return Respond(request, nas, ma == true);
    }

    private async Task<Guid?> ResolveSubscriberAsync(RadiusPacket request, NasInfo nas, string? username, CancellationToken ct)
    {
        // Class carries "nxt:<subscriber id>" from our Access-Accept.
        var cls = request.GetString(RadiusAttributeType.Class);
        if (cls != null && cls.StartsWith("nxt:", StringComparison.Ordinal) && Guid.TryParseExact(cls[4..], "N", out var id)
            && await _db.Subscribers.AnyAsync(s => s.Id == id && s.OrganizationId == nas.OrganizationId, ct))
            return id;
        if (string.IsNullOrEmpty(username)) return null;
        var cred = await _db.RadiusCredentials.AsNoTracking().Where(c => c.Username == username && c.OrganizationId == nas.OrganizationId).Select(c => (Guid?)c.SubscriberId).FirstOrDefaultAsync(ct);
        if (cred != null) return cred;
        var normalized = username.Trim().ToUpperInvariant();
        return await _db.Subscribers.AsNoTracking().Where(s => s.OrganizationId == nas.OrganizationId && s.NormalizedIdentifier == normalized).Select(s => (Guid?)s.Id).FirstOrDefaultAsync(ct);
    }

    public static string? TerminateCause(uint? code) => code switch
    {
        null => null,
        1 => "User-Request", 2 => "Lost-Carrier", 3 => "Lost-Service", 4 => "Idle-Timeout", 5 => "Session-Timeout", 6 => "Admin-Reset",
        7 => "Admin-Reboot", 8 => "Port-Error", 9 => "NAS-Error", 10 => "NAS-Request", 11 => "NAS-Reboot", 12 => "Port-Unneeded",
        13 => "Port-Preempted", 14 => "Port-Suspended", 15 => "Service-Unavailable", 16 => "Callback", 17 => "User-Error", 18 => "Host-Request",
        _ => $"Cause-{code}",
    };

    private static byte[] Respond(RadiusPacket request, NasInfo nas, bool sign) =>
        RadiusCrypto.EncodeResponse(new RadiusPacket(RadiusCode.AccountingResponse, request.Identifier), request, nas.Secret, sign);

    private async Task<byte[]?> DropAsync(NasInfo? nas, IPEndPoint remote, string? username, string reason, CancellationToken ct)
    {
        Interlocked.Increment(ref _counters.Dropped);
        _logger.LogWarning("Dropped Accounting-Request from {Ip}: {Reason}", remote.Address, reason);
        if (nas != null)
        {
            _db.RadiusRequestLogs.Add(RadiusLog.Entry(nas, remote.Address, RadiusPacketKind.AccountingRequest, RadiusResult.Dropped, username, null, reason));
            await _db.SaveChangesAsync(ct);
        }
        return null;
    }
}
