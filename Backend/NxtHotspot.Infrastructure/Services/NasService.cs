using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NxtHotspot.Application.Common;
using NxtHotspot.Application.Dtos;
using NxtHotspot.Domain.Entities;
using NxtHotspot.Domain.Enums;
using NxtHotspot.Infrastructure.Hotspot;
using NxtHotspot.Infrastructure.Persistence;

namespace NxtHotspot.Infrastructure.Services;

public sealed class NasService : INasService
{
    private readonly NxtDbContext _db;
    private readonly ICurrentUser _current;
    private readonly ITenantAccess _tenant;
    private readonly ISecretProtector _protector;
    private readonly RadiusOptions _radius;

    public NasService(NxtDbContext db, ICurrentUser current, ITenantAccess tenant, ISecretProtector protector, IOptions<RadiusOptions> radius)
    {
        _db = db; _current = current; _tenant = tenant; _protector = protector; _radius = radius.Value;
    }

    public async Task<List<NasDto>> ListAsync(Guid organizationId, Guid? siteId, CancellationToken ct = default)
    {
        var sites = await _tenant.AccessibleSiteIdsAsync(organizationId, ct);
        var q = _db.NasDevices.AsNoTracking().Where(n => n.OrganizationId == organizationId);
        if (sites != null) q = q.Where(n => sites.Contains(n.SiteId));
        if (siteId != null) q = q.Where(n => n.SiteId == siteId);
        var rows = await q.OrderBy(n => n.Site!.Name).ThenBy(n => n.Name).Select(n => new { n, SiteName = n.Site!.Name }).ToListAsync(ct);
        var ids = rows.Select(r => r.n.Id).ToList();
        var active = await _db.RadiusSessions.AsNoTracking().Where(s => ids.Contains(s.NasId) && s.Status == RadiusSessionStatus.Active)
            .GroupBy(s => s.NasId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        return rows.Select(r => Map(r.n, r.SiteName, active.GetValueOrDefault(r.n.Id))).ToList();
    }

    public async Task<NasDto> CreateAsync(Guid organizationId, UpsertNasRequest request, CancellationToken ct = default)
    {
        await HotspotScope.EnsureWritableAsync(_tenant, _current, organizationId, request.SiteId, ct);
        var org = await _db.Organizations.Include(o => o.SubscriptionPlan).FirstOrDefaultAsync(o => o.Id == organizationId, ct)
                  ?? throw new NotFoundException("Organization not found.");
        var limit = org.SubscriptionPlan?.MaxNasDevices;
        if (limit != null && await _db.NasDevices.CountAsync(n => n.OrganizationId == organizationId, ct) >= limit)
            throw new ConflictException("The subscription plan's NAS device limit has been reached.");

        var secret = request.SharedSecret?.Trim() ?? "";
        ValidateSecret(secret, required: true);
        var ip = NormalizeIp(request.IpAddress);
        await EnsureIpFreeAsync(ip, null, ct);

        var nas = new NasDevice { OrganizationId = organizationId };
        Apply(nas, request, ip);
        nas.SharedSecretEncrypted = _protector.Protect(secret);
        _db.NasDevices.Add(nas);
        _db.AuditLogs.Add(Audit.Entry(organizationId, _current.AdminUserId, "NasCreated", nameof(NasDevice), nas.Id.ToString(), $"Name={nas.Name}, Ip={ip}"));
        await SaveAsync(ct);
        return await GetAsync(organizationId, nas.Id, ct);
    }

    public async Task<NasDto> UpdateAsync(Guid organizationId, Guid nasId, UpsertNasRequest request, CancellationToken ct = default)
    {
        var nas = await LoadWritableAsync(organizationId, nasId, ct);
        await HotspotScope.EnsureWritableAsync(_tenant, _current, organizationId, request.SiteId, ct);
        var ip = NormalizeIp(request.IpAddress);
        await EnsureIpFreeAsync(ip, nasId, ct);
        Apply(nas, request, ip);
        var secretChanged = !string.IsNullOrWhiteSpace(request.SharedSecret);
        if (secretChanged)
        {
            ValidateSecret(request.SharedSecret!.Trim(), required: true);
            nas.SharedSecretEncrypted = _protector.Protect(request.SharedSecret.Trim());
        }
        _db.AuditLogs.Add(Audit.Entry(organizationId, _current.AdminUserId, "NasUpdated", nameof(NasDevice), nas.Id.ToString(),
            $"Name={nas.Name}, Ip={ip}{(secretChanged ? ", SecretChanged" : "")}"));
        await SaveAsync(ct);
        return await GetAsync(organizationId, nas.Id, ct);
    }

    public async Task DeleteAsync(Guid organizationId, Guid nasId, CancellationToken ct = default)
    {
        var nas = await LoadWritableAsync(organizationId, nasId, ct);
        if (await _db.RadiusSessions.AnyAsync(s => s.NasId == nasId, ct))
            throw new ConflictException("This NAS has session history. Deactivate it instead of deleting it.");
        _db.NasDevices.Remove(nas);
        _db.AuditLogs.Add(Audit.Entry(organizationId, _current.AdminUserId, "NasDeleted", nameof(NasDevice), nas.Id.ToString(), $"Name={nas.Name}"));
        await _db.SaveChangesAsync(ct);
    }

    public async Task<PortalKeyDto> RegeneratePortalKeyAsync(Guid organizationId, Guid nasId, CancellationToken ct = default)
    {
        var nas = await LoadWritableAsync(organizationId, nasId, ct);
        var key = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant(); // 128-bit
        nas.PortalKeyHash = HashKey(key);
        _db.AuditLogs.Add(Audit.Entry(organizationId, _current.AdminUserId, "NasPortalKeyRegenerated", nameof(NasDevice), nas.Id.ToString()));
        await _db.SaveChangesAsync(ct);
        return new PortalKeyDto(nas.Id, key, PortalUrl(key));
    }

    public async Task<RouterConfigDto> GetRouterConfigAsync(Guid organizationId, Guid nasId, CancellationToken ct = default)
    {
        var sites = await _tenant.AccessibleSiteIdsAsync(organizationId, ct);
        var nas = await _db.NasDevices.AsNoTracking().FirstOrDefaultAsync(n => n.Id == nasId && n.OrganizationId == organizationId, ct)
                  ?? throw new NotFoundException("NAS device not found.");
        if (sites != null && !sites.Contains(nas.SiteId)) throw new NotFoundException("NAS device not found.");

        var server = string.IsNullOrWhiteSpace(_radius.PublicAddress) ? "<NXT_RADIUS_SERVER_IP>" : _radius.PublicAddress;
        var portalHost = Uri.TryCreate(_radius.PortalBaseUrl, UriKind.Absolute, out var u) ? u.Host : "<PORTAL_HOST>";
        var v7 = string.Equals(nas.RouterOsVersion, "v7", StringComparison.OrdinalIgnoreCase);
        var sb = new StringBuilder();
        sb.AppendLine($"# NXT Hotspot – RADIUS configuration for {nas.Name} ({nas.IpAddress})");
        sb.AppendLine($"# RouterOS {(v7 ? "v7" : "v6")}. Replace <SHARED_SECRET> with the secret saved for this NAS (it is never displayed by NXT).");
        sb.AppendLine($"/radius add service=hotspot address={server} secret=\"<SHARED_SECRET>\" authentication-port={_radius.AuthPort} accounting-port={_radius.AcctPort} timeout=3s comment=\"NXT Hotspot\"");
        sb.AppendLine($"/radius incoming set accept=yes port={nas.CoaPort}");
        sb.AppendLine("/ip hotspot profile set [find default=yes] use-radius=yes radius-accounting=yes radius-interim-update=5m login-by=http-pap,http-chap");
        sb.AppendLine($"/ip hotspot walled-garden add dst-host={portalHost} comment=\"NXT captive portal\"");
        if (v7) sb.AppendLine("# RouterOS v7: if your build enforces Message-Authenticator, NXT signs every reply with it.");
        var notes = new List<string>
        {
            "Generated from RouterOS documentation. It has not been verified against your router model and RouterOS build: test before production use.",
            "The router's source IP must match the NAS IP address saved in NXT, otherwise requests are dropped.",
            "Captive portal: generate a portal key, then replace the router's hotspot/login.html with a page that redirects to " +
                "<PORTAL_URL>?mac=$(mac)&ip=$(ip)&link-login-only=$(link-login-only)&dst=$(link-orig-esc) (MikroTik variables). " +
                "After sign-in the portal posts a one-time username/password to link-login-only.",
        };
        if (string.IsNullOrWhiteSpace(_radius.PublicAddress)) notes.Add("Set Radius:PublicAddress in the API configuration so the server address is filled in automatically.");
        return new RouterConfigDto(nas.Id, v7 ? "v7" : "v6", sb.ToString(), notes);
    }

    public static string HashKey(string key) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant();

    private string PortalUrl(string key) =>
        string.IsNullOrWhiteSpace(_radius.PortalBaseUrl) ? $"/p/{key}" : $"{_radius.PortalBaseUrl.TrimEnd('/')}/p/{key}";

    private async Task<NasDevice> LoadWritableAsync(Guid organizationId, Guid nasId, CancellationToken ct)
    {
        _tenant.EnsureOrganization(organizationId);
        var nas = await _db.NasDevices.FirstOrDefaultAsync(n => n.Id == nasId && n.OrganizationId == organizationId, ct)
                  ?? throw new NotFoundException("NAS device not found.");
        await HotspotScope.EnsureWritableAsync(_tenant, _current, organizationId, nas.SiteId, ct);
        return nas;
    }

    private async Task<NasDto> GetAsync(Guid organizationId, Guid id, CancellationToken ct) =>
        (await ListAsync(organizationId, null, ct)).First(n => n.Id == id);

    private async Task EnsureIpFreeAsync(string ip, Guid? exceptId, CancellationToken ct)
    {
        if (await _db.NasDevices.AnyAsync(n => n.IpAddress == ip && n.Id != exceptId, ct))
            throw new ConflictException("Another NAS already uses this IP address. RADIUS identifies routers by source IP, so it must be unique.");
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { throw new ConflictException("Another NAS already uses this IP address."); }
    }

    private static string NormalizeIp(string value)
    {
        if (!IPAddress.TryParse(value.Trim(), out var ip)) throw new ValidationFailedException("Enter a valid IPv4 or IPv6 address.");
        return ip.ToString();
    }

    private static void ValidateSecret(string secret, bool required)
    {
        if (string.IsNullOrEmpty(secret)) { if (required) throw new ValidationFailedException("A shared secret is required."); return; }
        if (secret.Length < 12) throw new ValidationFailedException("The shared secret must be at least 12 characters.");
        if (secret.Any(char.IsControl)) throw new ValidationFailedException("The shared secret contains invalid characters.");
    }

    private static void Apply(NasDevice nas, UpsertNasRequest r, string ip)
    {
        nas.SiteId = r.SiteId!.Value;
        nas.Name = r.Name.Trim();
        nas.IpAddress = ip;
        nas.Vendor = r.Vendor;
        nas.RouterOsVersion = string.IsNullOrWhiteSpace(r.RouterOsVersion) ? null : r.RouterOsVersion.Trim();
        nas.AuthPort = r.AuthPort;
        nas.AcctPort = r.AcctPort;
        nas.CoaPort = r.CoaPort;
        nas.Description = string.IsNullOrWhiteSpace(r.Description) ? null : r.Description.Trim();
        nas.IsActive = r.IsActive;
    }

    private static NasDto Map(NasDevice n, string siteName, int activeSessions) => new(n.Id, n.OrganizationId, n.SiteId, siteName, n.Name, n.IpAddress,
        n.Vendor, n.RouterOsVersion, n.AuthPort, n.AcctPort, n.CoaPort, n.Description, n.IsActive, !string.IsNullOrEmpty(n.SharedSecretEncrypted),
        n.PortalKeyHash != null, n.LastSeenAt, activeSessions, n.CreatedAt);
}
