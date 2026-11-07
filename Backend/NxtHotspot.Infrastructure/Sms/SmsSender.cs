using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NxtHotspot.Application.Common;
using NxtHotspot.Domain.Entities;
using NxtHotspot.Infrastructure.Persistence;

namespace NxtHotspot.Infrastructure.Sms;

public sealed record SmsSendResult(bool Success, string? Error, bool DevelopmentLog);

public interface ISmsSender
{
    /// <summary>Sends a message through the organization's resolved provider (site override → organization → platform default).</summary>
    Task<SmsSendResult> SendOtpAsync(Guid organizationId, Guid siteId, string mobile, string code, int expiryMinutes, CancellationToken ct);
}

/// <summary>
/// 2Factor-style HTTP SMS sender: GET {endpoint}?module=TRANS_SMS&amp;apikey=…&amp;to=…&amp;from=…&amp;msg=…
/// The endpoint host must be on Sms:AllowedHosts (SSRF protection). Keys and codes are never logged.
/// </summary>
public sealed class SmsSender : ISmsSender
{
    private readonly NxtDbContext _db;
    private readonly ISmsSettingResolver _resolver;
    private readonly ISecretProtector _protector;
    private readonly IHttpClientFactory _http;
    private readonly SmsOptions _options;
    private readonly ILogger<SmsSender> _logger;

    public SmsSender(NxtDbContext db, ISmsSettingResolver resolver, ISecretProtector protector, IHttpClientFactory http, IOptions<SmsOptions> options, ILogger<SmsSender> logger)
    {
        _db = db; _resolver = resolver; _protector = protector; _http = http; _options = options.Value; _logger = logger;
    }

    public static string Mask(string mobile) => mobile.Length <= 4 ? "****" : new string('*', mobile.Length - 4) + mobile[^4..];

    public async Task<SmsSendResult> SendOtpAsync(Guid organizationId, Guid siteId, string mobile, string code, int expiryMinutes, CancellationToken ct)
    {
        var setting = await _resolver.ResolveAsync(organizationId, siteId, ct);
        var template = string.IsNullOrWhiteSpace(setting?.MessageTemplate) ? _options.DefaultMessageTemplate : setting!.MessageTemplate;
        var message = template.Replace("{OTP}", code).Replace("{EXPIRY}", expiryMinutes.ToString());

        if (_options.IsDevelopmentLog)
        {
            _logger.LogWarning("DEVELOPMENT SMS (not sent) to {Mobile}: {Message}", Mask(mobile), message);
            Log(organizationId, siteId, mobile, "DevelopmentLog", true, "Development mode: not sent");
            await _db.SaveChangesAsync(ct);
            return new SmsSendResult(true, null, true);
        }

        if (setting == null) return await FailAsync(organizationId, siteId, mobile, "none", "No active SMS provider is configured", ct);
        var endpoint = string.IsNullOrWhiteSpace(setting.ApiEndpoint) ? _options.DefaultEndpoint : setting.ApiEndpoint;
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps
            || !_options.EffectiveAllowedHosts.Any(h => string.Equals(h, uri.Host, StringComparison.OrdinalIgnoreCase)))
            return await FailAsync(organizationId, siteId, mobile, setting.ProviderName, "SMS endpoint is not on the allowed host list", ct);

        string key;
        try { key = _protector.Unprotect(setting.ApiKeyEncrypted); }
        catch { return await FailAsync(organizationId, siteId, mobile, setting.ProviderName, "Stored SMS API key cannot be decrypted (encryption key changed?)", ct); }

        var url = $"{endpoint.TrimEnd('?')}{(endpoint.Contains('?') ? "&" : "?")}module=TRANS_SMS&apikey={Uri.EscapeDataString(key)}&to={Uri.EscapeDataString(mobile)}" +
                  $"&from={Uri.EscapeDataString(setting.SenderId)}&msg={Uri.EscapeDataString(message)}";
        try
        {
            var client = _http.CreateClient("sms");
            using var response = await client.GetAsync(url, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            var ok = response.IsSuccessStatusCode && body.Contains("success", StringComparison.OrdinalIgnoreCase);
            if (!ok) return await FailAsync(organizationId, siteId, mobile, setting.ProviderName, $"Provider answered HTTP {(int)response.StatusCode}", ct);
            Log(organizationId, siteId, mobile, setting.ProviderName, true, null);
            await _db.SaveChangesAsync(ct);
            return new SmsSendResult(true, null, false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return await FailAsync(organizationId, siteId, mobile, setting.ProviderName, $"Provider unreachable: {ex.GetType().Name}", ct);
        }
    }

    private async Task<SmsSendResult> FailAsync(Guid orgId, Guid siteId, string mobile, string provider, string error, CancellationToken ct)
    {
        _logger.LogWarning("SMS to {Mobile} failed: {Error}", Mask(mobile), error);
        Log(orgId, siteId, mobile, provider, false, error);
        await _db.SaveChangesAsync(ct);
        return new SmsSendResult(false, error, false);
    }

    private void Log(Guid orgId, Guid siteId, string mobile, string provider, bool success, string? error) =>
        _db.SmsLogs.Add(new SmsLog { OrganizationId = orgId, SiteId = siteId, MobileMasked = Mask(mobile), ProviderName = provider, Success = success, Error = error });
}
