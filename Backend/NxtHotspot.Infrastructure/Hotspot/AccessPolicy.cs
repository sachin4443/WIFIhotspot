using Microsoft.EntityFrameworkCore;
using NxtHotspot.Domain.Entities;
using NxtHotspot.Domain.Enums;
using NxtHotspot.Infrastructure.Persistence;

namespace NxtHotspot.Infrastructure.Hotspot;

/// <summary>Outcome of the hotspot access rules: a reject reason, or the limits to send to the router.</summary>
public sealed record AccessDecision(string? Reason, int? SessionTimeout = null, int? IdleTimeout = null, string? RateLimit = null, long? RemainingBytes = null)
{
    public bool Allowed => Reason == null;
}

/// <summary>
/// Hotspot access rules shared by the captive portal (before issuing a credential) and the RADIUS server
/// (on every Access-Request), so both always agree.
/// </summary>
public static class AccessPolicy
{
    public static async Task<AccessDecision> EvaluateAsync(NxtDbContext db, Subscriber s, Guid organizationId, Guid siteId, string? mac, DateTime now, CancellationToken ct)
    {
        var org = await db.Organizations.AsNoTracking().Include(o => o.Settings).FirstOrDefaultAsync(o => o.Id == organizationId, ct);
        if (org == null || org.Status != OrganizationStatus.Active) return new("Organization is not active");
        if (!await db.Sites.AnyAsync(x => x.Id == siteId && x.OrganizationId == organizationId && x.IsActive, ct)) return new("Site is inactive");
        if (s.OrganizationId != organizationId) return new("Unknown user");
        if (s.Status != SubscriberStatus.Active) return new($"Account is {s.Status.ToString().ToLowerInvariant()}");
        if (s.SiteId != null && s.SiteId != siteId) return new("Not allowed at this site");

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

        var pkg = s.PackageId == null ? null : await db.InternetPackages.AsNoTracking().FirstOrDefaultAsync(p => p.Id == s.PackageId, ct);
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
            var used = await db.RadiusSessions.Where(x => x.SubscriberId == s.Id && x.StartedAt >= since).SumAsync(x => (long?)(x.InputBytes + x.OutputBytes), ct) ?? 0;
            remaining = mb * 1024L * 1024L - used;
            if (remaining <= 0) return new("Data quota exhausted");
        }

        var maxDevices = s.MaxDevices ?? pkg?.MaxDevices ?? org.Settings?.DefaultMaxDevices ?? 1;
        var active = await db.RadiusSessions.AsNoTracking().Where(x => x.SubscriberId == s.Id && x.Status == RadiusSessionStatus.Active)
            .Select(x => x.CallingStationId).ToListAsync(ct);
        var sameDevice = mac != null && active.Any(m => string.Equals(NormalizeMac(m), mac, StringComparison.OrdinalIgnoreCase));
        if (!sameDevice && active.Count >= maxDevices) return new($"Device limit reached ({maxDevices})");

        int? timeout = limits.Count > 0 ? Math.Max(60, (int)Math.Floor(limits.Min())) : null;
        string? rate = pkg != null && (pkg.DownloadKbps > 0 || pkg.UploadKbps > 0) ? $"{pkg.UploadKbps}k/{pkg.DownloadKbps}k" : null;
        return new(null, timeout, pkg?.IdleTimeoutMinutes * 60, rate, remaining);
    }

    /// <summary>"AA-BB-CC-DD-EE-FF", "aa:bb:…" or "aabb.ccdd.eeff" -> "AA:BB:CC:DD:EE:FF"; null if not a MAC.</summary>
    public static string? NormalizeMac(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var hex = new string(raw.Where(Uri.IsHexDigit).ToArray());
        if (hex.Length != 12) return null;
        return string.Join(":", Enumerable.Range(0, 6).Select(i => hex.Substring(i * 2, 2))).ToUpperInvariant();
    }
}
