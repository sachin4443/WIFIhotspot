using System.Collections.Concurrent;
using System.Net;
using Microsoft.EntityFrameworkCore;
using NxtHotspot.Application.Common;
using NxtHotspot.Domain.Entities;
using NxtHotspot.Infrastructure.Persistence;

namespace NxtHotspot.Radius.Application;

/// <summary>A NAS resolved from a packet's source IP, with its decrypted shared secret.</summary>
public sealed record NasInfo(Guid Id, Guid OrganizationId, Guid SiteId, string Name, string IpAddress, int CoaPort, byte[] Secret, bool IsActive);

/// <summary>Live counters reported in the heartbeat.</summary>
public sealed class RadiusCounters
{
    public long AuthRequests, AuthAccepts, AuthRejects, AcctRequests, Dropped;
}

/// <summary>Resolves NAS devices by source IP. Secrets are decrypted once and cached briefly.</summary>
public sealed class NasDirectory
{
    private readonly ConcurrentDictionary<string, (NasInfo? Nas, DateTime Expires)> _cache = new();
    private readonly ISecretProtector _protector;
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(30);

    public NasDirectory(ISecretProtector protector) => _protector = protector;

    public async Task<NasInfo?> FindAsync(NxtDbContext db, IPAddress source, CancellationToken ct)
    {
        var ip = (source.IsIPv4MappedToIPv6 ? source.MapToIPv4() : source).ToString();
        if (_cache.TryGetValue(ip, out var hit) && hit.Expires > DateTime.UtcNow) return hit.Nas;
        var nas = await db.NasDevices.AsNoTracking().FirstOrDefaultAsync(n => n.IpAddress == ip, ct);
        NasInfo? info = null;
        if (nas != null)
        {
            byte[] secret;
            try { secret = System.Text.Encoding.UTF8.GetBytes(_protector.Unprotect(nas.SharedSecretEncrypted)); }
            catch { secret = Array.Empty<byte>(); }
            info = new NasInfo(nas.Id, nas.OrganizationId, nas.SiteId, nas.Name, nas.IpAddress, nas.CoaPort, secret, nas.IsActive && secret.Length > 0);
        }
        _cache[ip] = (info, DateTime.UtcNow + Ttl);
        return info;
    }

    public void Invalidate() => _cache.Clear();
}

internal static class RadiusLog
{
    public static RadiusRequestLog Entry(NasInfo? nas, IPAddress source, Domain.Enums.RadiusPacketKind kind, Domain.Enums.RadiusResult result,
        string? username, string? mac, string? reason) => new()
    {
        OrganizationId = nas?.OrganizationId,
        SiteId = nas?.SiteId,
        NasId = nas?.Id,
        SourceIp = source.ToString(),
        PacketKind = kind,
        Result = result,
        Username = username?.Length > 100 ? username[..100] : username,
        MacAddress = mac?.Length > 64 ? mac[..64] : mac,
        Reason = reason?.Length > 300 ? reason[..300] : reason,
    };

    public static string? NormalizeMac(string? raw) => NxtHotspot.Infrastructure.Hotspot.AccessPolicy.NormalizeMac(raw);
}
