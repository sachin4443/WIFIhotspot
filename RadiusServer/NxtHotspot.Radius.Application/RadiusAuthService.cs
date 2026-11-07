using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NxtHotspot.Application.Common;
using NxtHotspot.Domain.Entities;
using NxtHotspot.Domain.Enums;
using NxtHotspot.Infrastructure.Persistence;
using NxtHotspot.RadiusProtocol;

namespace NxtHotspot.Radius.Application;

/// <summary>
/// Access-Request pipeline: identify NAS → validate packet → identify user (one-time portal credential or subscriber
/// username/password) → verify credential → check status, stay, validity, quota and device limit → Accept with
/// Session-Timeout, Idle-Timeout, Mikrotik-Rate-Limit and Mikrotik-Total-Limit, or Reject. Passwords are never logged.
/// </summary>
public sealed class RadiusAuthService
{
    public const int InterimIntervalSeconds = 300;

    private readonly NxtDbContext _db;
    private readonly NasDirectory _nas;
    private readonly IPasswordService _passwords;
    private readonly RadiusCounters _counters;
    private readonly ILogger<RadiusAuthService> _logger;

    public RadiusAuthService(NxtDbContext db, NasDirectory nas, IPasswordService passwords, RadiusCounters counters, ILogger<RadiusAuthService> logger)
    {
        _db = db; _nas = nas; _passwords = passwords; _counters = counters; _logger = logger;
    }

    /// <summary>Returns the encoded response, or null when the packet must be silently discarded.</summary>
    public async Task<byte[]?> HandleAsync(byte[] datagram, IPEndPoint remote, CancellationToken ct)
    {
        Interlocked.Increment(ref _counters.AuthRequests);
        var nas = await _nas.FindAsync(_db, remote.Address, ct);
        RadiusPacket request;
        try { request = RadiusPacket.Parse(datagram); }
        catch (RadiusPacketException ex) { return await DropAsync(nas, remote, null, null, $"Malformed packet: {ex.Message}", ct); }

        if (request.Code != RadiusCode.AccessRequest) return await DropAsync(nas, remote, null, null, $"Unexpected code {request.Code} on auth port", ct);
        var username = request.GetString(RadiusAttributeType.UserName);
        var mac = RadiusLog.NormalizeMac(request.GetString(RadiusAttributeType.CallingStationId)) ?? request.GetString(RadiusAttributeType.CallingStationId);
        if (nas == null) return await DropAsync(null, remote, username, mac, "Unknown NAS (source IP not registered)", ct);
        if (!nas.IsActive) return await DropAsync(nas, remote, username, mac, "NAS is inactive or has no shared secret", ct);

        var ma = RadiusCrypto.VerifyMessageAuthenticator(request, nas.Secret);
        if (ma == false) return await DropAsync(nas, remote, username, mac, "Invalid Message-Authenticator (check the shared secret)", ct);
        var signReply = ma == true;

        if (request.Has(RadiusAttributeType.EapMessage)) return await RejectAsync(request, nas, remote, username, mac, "EAP is not supported", signReply, ct);
        if (string.IsNullOrEmpty(username)) return await RejectAsync(request, nas, remote, username, mac, "Missing User-Name", signReply, ct);

        string? papPassword = null;
        byte[]? chap = null;
        if (request.Get(RadiusAttributeType.UserPassword) is { } pw)
        {
            try { papPassword = RadiusCrypto.DecryptPap(pw.Value, nas.Secret, request.Authenticator); }
            catch (RadiusPacketException) { return await DropAsync(nas, remote, username, mac, "Malformed User-Password", ct); }
        }
        else if (request.Get(RadiusAttributeType.ChapPassword) is { } c) chap = c.Value;
        else return await RejectAsync(request, nas, remote, username, mac, "No password supplied", signReply, ct);

        var now = DateTime.UtcNow;
        Subscriber? subscriber;
        RadiusCredential? credential = await _db.RadiusCredentials.Include(x => x.Subscriber).FirstOrDefaultAsync(x => x.Username == username, ct);

        if (credential != null)
        {
            if (credential.OrganizationId != nas.OrganizationId || credential.SiteId != nas.SiteId)
                return await RejectAsync(request, nas, remote, username, mac, "Credential belongs to another site", signReply, ct);
            if (papPassword == null) return await RejectAsync(request, nas, remote, username, mac, "One-time credentials require PAP (enable http-pap on the hotspot)", signReply, ct);
            if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(credential.PasswordHash), SHA256.HashData(Encoding.UTF8.GetBytes(papPassword))))
                return await RejectAsync(request, nas, remote, username, mac, "Wrong password", signReply, ct);
            if (credential.UsedAt != null) return await RejectAsync(request, nas, remote, username, mac, "One-time credential already used", signReply, ct);
            if (credential.ExpiresAt <= now) return await RejectAsync(request, nas, remote, username, mac, "One-time credential expired", signReply, ct);
            if (credential.MacAddress != null && mac != null && !string.Equals(credential.MacAddress, mac, StringComparison.OrdinalIgnoreCase))
                return await RejectAsync(request, nas, remote, username, mac, "Device does not match the portal sign-in", signReply, ct);
            subscriber = credential.Subscriber;
        }
        else
        {
            var normalized = username.Trim().ToUpperInvariant();
            subscriber = await _db.Subscribers.FirstOrDefaultAsync(s => s.OrganizationId == nas.OrganizationId && s.NormalizedIdentifier == normalized, ct);
            if (subscriber?.PasswordHash == null)
            {
                _passwords.VerifyDummy(papPassword ?? "x");
                return await RejectAsync(request, nas, remote, username, mac, "Unknown user", signReply, ct);
            }
            if (papPassword == null)
                return await RejectAsync(request, nas, remote, username, mac, "CHAP cannot be verified against hashed passwords; enable http-pap", signReply, ct);
            if (!_passwords.Verify(subscriber.PasswordHash, papPassword))
                return await RejectAsync(request, nas, remote, username, mac, "Wrong password", signReply, ct);
        }
        if (subscriber == null) return await RejectAsync(request, nas, remote, username, mac, "Unknown user", signReply, ct);

        var decision = await AuthorizeAsync(subscriber, nas, mac, now, ct);
        if (decision.Reason != null) return await RejectAsync(request, nas, remote, username, mac, decision.Reason, signReply, ct);

        var accept = new RadiusPacket(RadiusCode.AccessAccept, request.Identifier);
        accept.Add(RadiusAttribute.String(RadiusAttributeType.Class, $"nxt:{subscriber.Id:N}"));
        accept.Add(RadiusAttribute.Integer(RadiusAttributeType.AcctInterimInterval, InterimIntervalSeconds));
        if (decision.SessionTimeout is { } st) accept.Add(RadiusAttribute.Integer(RadiusAttributeType.SessionTimeout, (uint)st));
        if (decision.IdleTimeout is { } it) accept.Add(RadiusAttribute.Integer(RadiusAttributeType.IdleTimeout, (uint)it));
        if (decision.RateLimit != null) accept.Add(RadiusAttribute.VendorString(MikrotikAttribute.VendorId, MikrotikAttribute.RateLimit, decision.RateLimit));
        if (decision.RemainingBytes is { } rb)
        {
            accept.Add(RadiusAttribute.VendorInteger(MikrotikAttribute.VendorId, MikrotikAttribute.TotalLimit, (uint)(rb & 0xFFFFFFFF)));
            if (rb > uint.MaxValue) accept.Add(RadiusAttribute.VendorInteger(MikrotikAttribute.VendorId, MikrotikAttribute.TotalLimitGigawords, (uint)(rb >> 32)));
        }

        if (credential != null) credential.UsedAt = now;
        subscriber.FirstLoginAt ??= now;
        subscriber.LastLoginAt = now;
        await TouchNasAsync(nas, ct);
        _db.RadiusRequestLogs.Add(RadiusLog.Entry(nas, remote.Address, RadiusPacketKind.AccessRequest, RadiusResult.Accept, username, mac,
            $"Accepted ({subscriber.SubscriberType}{(credential != null ? ", portal credential" : "")})"));
        await _db.SaveChangesAsync(ct);
        Interlocked.Increment(ref _counters.AuthAccepts);
        return RadiusCrypto.EncodeResponse(accept, request, nas.Secret, addMessageAuthenticator: true);
    }

    public sealed record Decision(string? Reason, int? SessionTimeout = null, int? IdleTimeout = null, string? RateLimit = null, long? RemainingBytes = null);

    /// <summary>Business rules shared by direct and portal logins.</summary>
    public async Task<Decision> AuthorizeAsync(Subscriber s, NasInfo nas, string? mac, DateTime now, CancellationToken ct)
    {
        var org = await _db.Organizations.AsNoTracking().Include(o => o.Settings).FirstAsync(o => o.Id == nas.OrganizationId, ct);
        if (org.Status != OrganizationStatus.Active) return new("Organization is not active");
        if (!await _db.Sites.AnyAsync(x => x.Id == nas.SiteId && x.IsActive, ct)) return new("Site is inactive");
        if (s.OrganizationId != nas.OrganizationId) return new("Unknown user");
        if (s.Status != SubscriberStatus.Active) return new($"Account is {s.Status.ToString().ToLowerInvariant()}");
        if (s.SiteId != null && s.SiteId != nas.SiteId) return new("Not allowed at this site");

        var limits = new List<double>();
        if (s.SubscriberType == SubscriberType.RoomGuest)
        {
            if (s.CheckInAt != null && now < s.CheckInAt) return new("Stay has not started");
            if (s.CheckOutAt == null || now >= s.CheckOutAt) return new("Stay has ended");
            limits.Add((s.CheckOutAt.Value - now).TotalSeconds);
        }
        if (s.ValidUntil != null)
        {
            if (now >= s.ValidUntil) return new("Access has expired");
            limits.Add((s.ValidUntil.Value - now).TotalSeconds);
        }

        var pkg = s.PackageId == null ? null : await _db.InternetPackages.AsNoTracking().FirstOrDefaultAsync(p => p.Id == s.PackageId, ct);
        if (pkg != null && !pkg.IsActive) return new("Package is inactive");
        if (pkg?.ValidityMinutes is { } validity && s.FirstLoginAt is { } first)
        {
            var end = first.AddMinutes(validity);
            if (now >= end) return new("Package validity has ended");
            limits.Add((end - now).TotalSeconds);
        }
        if (pkg?.SessionTimeoutMinutes is { } sess) limits.Add(sess * 60);
        if (pkg == null && org.Settings != null) limits.Add(org.Settings.SessionValiditySeconds);

        long? remaining = null;
        var limitMb = pkg?.DataLimitMb ?? org.Settings?.DefaultDataLimitMb;
        if (limitMb is { } mb)
        {
            // Accounting start times are reconstructed from Acct-Session-Time/Delay and can precede the first Accept by seconds
            // (clock skew, delayed Start), so the usage window opens slightly before the first login.
            var since = s.FirstLoginAt?.AddMinutes(-10) ?? DateTime.MinValue;
            var used = await _db.RadiusSessions.Where(x => x.SubscriberId == s.Id && x.StartedAt >= since).SumAsync(x => (long?)(x.InputBytes + x.OutputBytes), ct) ?? 0;
            remaining = mb * 1024L * 1024L - used;
            if (remaining <= 0) return new("Data quota exhausted");
        }

        var maxDevices = s.MaxDevices ?? pkg?.MaxDevices ?? org.Settings?.DefaultMaxDevices ?? 1;
        var active = await _db.RadiusSessions.AsNoTracking().Where(x => x.SubscriberId == s.Id && x.Status == RadiusSessionStatus.Active)
            .Select(x => x.CallingStationId).ToListAsync(ct);
        var sameDevice = mac != null && active.Any(m => string.Equals(RadiusLog.NormalizeMac(m), mac, StringComparison.OrdinalIgnoreCase));
        if (!sameDevice && active.Count >= maxDevices) return new($"Device limit reached ({maxDevices})");

        int? timeout = limits.Count > 0 ? Math.Max(60, (int)Math.Floor(limits.Min())) : null;
        string? rate = pkg != null && (pkg.DownloadKbps > 0 || pkg.UploadKbps > 0) ? $"{pkg.UploadKbps}k/{pkg.DownloadKbps}k" : null;
        int? idle = pkg?.IdleTimeoutMinutes * 60;
        return new(null, timeout, idle, rate, remaining);
    }

    private async Task TouchNasAsync(NasInfo nas, CancellationToken ct)
    {
        var entity = await _db.NasDevices.FirstOrDefaultAsync(n => n.Id == nas.Id, ct);
        if (entity != null) entity.LastSeenAt = DateTime.UtcNow;
    }

    private async Task<byte[]> RejectAsync(RadiusPacket request, NasInfo nas, IPEndPoint remote, string? username, string? mac, string reason, bool _, CancellationToken ct)
    {
        Interlocked.Increment(ref _counters.AuthRejects);
        await TouchNasAsync(nas, ct);
        _db.RadiusRequestLogs.Add(RadiusLog.Entry(nas, remote.Address, RadiusPacketKind.AccessRequest, RadiusResult.Reject, username, mac, reason));
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Access-Reject for {User} from {Nas}: {Reason}", username, nas.Name, reason);
        // The router may show Reply-Message to the user: never reveal whether an account exists.
        var visible = reason is "Unknown user" or "Wrong password" ? "Invalid username or password" : reason;
        var reject = new RadiusPacket(RadiusCode.AccessReject, request.Identifier).Add(RadiusAttribute.String(RadiusAttributeType.ReplyMessage, visible));
        return RadiusCrypto.EncodeResponse(reject, request, nas.Secret, addMessageAuthenticator: true);
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, DateTime> UnknownSourceLogged = new();

    private async Task<byte[]?> DropAsync(NasInfo? nas, IPEndPoint remote, string? username, string? mac, string reason, CancellationToken ct)
    {
        Interlocked.Increment(ref _counters.Dropped);
        if (nas == null)
        {
            // Unknown sources are logged at most once a minute per IP so a flood cannot fill the log table.
            var key = remote.Address.ToString();
            if (UnknownSourceLogged.TryGetValue(key, out var last) && DateTime.UtcNow - last < TimeSpan.FromMinutes(1)) return null;
            UnknownSourceLogged[key] = DateTime.UtcNow;
        }
        _logger.LogWarning("Dropped Access-Request from {Ip}: {Reason}", remote.Address, reason);
        _db.RadiusRequestLogs.Add(RadiusLog.Entry(nas, remote.Address, RadiusPacketKind.AccessRequest, RadiusResult.Dropped, username, mac, reason));
        await _db.SaveChangesAsync(ct);
        return null;
    }
}
