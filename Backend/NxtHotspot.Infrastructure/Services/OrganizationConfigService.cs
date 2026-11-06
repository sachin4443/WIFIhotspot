using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NxtHotspot.Application.Common;
using NxtHotspot.Application.Dtos;
using NxtHotspot.Domain.Entities;
using NxtHotspot.Infrastructure.Persistence;
using NxtHotspot.Infrastructure.Sms;

namespace NxtHotspot.Infrastructure.Services;

public sealed class OrganizationConfigService : IOrganizationConfigService
{
    private readonly NxtDbContext _db;
    private readonly ICurrentUser _current;
    private readonly ITenantAccess _tenant;
    private readonly ISecretProtector _protector;
    private readonly SmsOptions _sms;

    public OrganizationConfigService(NxtDbContext db, ICurrentUser current, ITenantAccess tenant,
        ISecretProtector protector, IOptions<SmsOptions> sms)
    {
        _db = db; _current = current; _tenant = tenant; _protector = protector; _sms = sms.Value;
    }

    // ---------------------------------------------------------------- scope helpers

    /// <summary>Organization-wide configuration may only be changed by platform admins or organization admins with access to all sites.</summary>
    private void EnsureOrgWide(Guid organizationId)
    {
        _tenant.EnsureOrganization(organizationId);
        if (!_current.IsPlatformAdmin && !_current.HasAllSites)
            throw new ForbiddenException("Only administrators with access to all sites can change organization-wide configuration.");
    }

    private async Task<Site> AuthorizeSiteInOrgAsync(Guid organizationId, Guid siteId, CancellationToken ct)
    {
        _tenant.EnsureOrganization(organizationId);
        var site = await _tenant.GetAuthorizedSiteAsync(siteId, ct);
        if (site.OrganizationId != organizationId) throw new NotFoundException("Site not found.");
        return site;
    }

    /// <summary>Organization-level scope needs org-wide rights; a site scope needs access to that site.</summary>
    private async Task AuthorizeScopeForWriteAsync(Guid organizationId, Guid? siteId, CancellationToken ct)
    {
        if (siteId == null) EnsureOrgWide(organizationId);
        else await AuthorizeSiteInOrgAsync(organizationId, siteId.Value, ct);
    }

    private async Task EnsureOrganizationExistsAsync(Guid organizationId, CancellationToken ct)
    {
        if (!await _db.Organizations.AnyAsync(o => o.Id == organizationId, ct))
            throw new NotFoundException("Organization not found.");
    }

    private void Log(Guid? orgId, string action, string entityType, string? entityId, string? details = null) =>
        _db.AuditLogs.Add(Audit.Entry(orgId, _current.AdminUserId, action, entityType, entityId, details));

    // ---------------------------------------------------------------- profile

    public async Task<OrganizationProfileDto> GetProfileAsync(Guid organizationId, CancellationToken ct = default)
    {
        _tenant.EnsureOrganization(organizationId);
        var o = await _db.Organizations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == organizationId, ct)
                ?? throw new NotFoundException("Organization not found.");
        return new OrganizationProfileDto(o.Id, o.Name, o.ContactPerson, o.MobileNumber, o.Email, o.Address);
    }

    public async Task<OrganizationProfileDto> UpdateProfileAsync(Guid organizationId, UpdateOrganizationProfileRequest r, CancellationToken ct = default)
    {
        EnsureOrgWide(organizationId);
        var o = await _db.Organizations.FirstOrDefaultAsync(x => x.Id == organizationId, ct)
                ?? throw new NotFoundException("Organization not found.");
        o.Name = r.Name.Trim();
        o.ContactPerson = r.ContactPerson.Trim();
        o.MobileNumber = r.MobileNumber.Trim();
        o.Email = r.Email.Trim();
        o.Address = string.IsNullOrWhiteSpace(r.Address) ? null : r.Address.Trim();
        Log(o.Id, "OrganizationProfileUpdated", nameof(Organization), o.Id.ToString());
        await _db.SaveChangesAsync(ct);
        return new OrganizationProfileDto(o.Id, o.Name, o.ContactPerson, o.MobileNumber, o.Email, o.Address);
    }

    // ---------------------------------------------------------------- settings

    public async Task<OrganizationSettingsDto> GetSettingsAsync(Guid organizationId, CancellationToken ct = default)
    {
        _tenant.EnsureOrganization(organizationId);
        var s = await _db.OrganizationSettings.AsNoTracking().FirstOrDefaultAsync(x => x.OrganizationId == organizationId, ct)
                ?? throw new NotFoundException("Organization settings not found.");
        return ToDto(s);
    }

    public async Task<OrganizationSettingsDto> UpdateSettingsAsync(Guid organizationId, UpdateOrganizationSettingsRequest r, CancellationToken ct = default)
    {
        EnsureOrgWide(organizationId);
        try { _ = TimeZoneInfo.FindSystemTimeZoneById(r.TimeZoneId.Trim()); }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        { throw new ValidationFailedException("Unknown time zone."); }

        var s = await _db.OrganizationSettings.FirstOrDefaultAsync(x => x.OrganizationId == organizationId, ct)
                ?? throw new NotFoundException("Organization settings not found.");
        s.OtpExpirySeconds = r.OtpExpirySeconds;
        s.OtpResendCooldownSeconds = r.OtpResendCooldownSeconds;
        s.OtpMaxAttempts = r.OtpMaxAttempts;
        s.SessionValiditySeconds = r.SessionValiditySeconds;
        s.DefaultMaxDevices = r.DefaultMaxDevices;
        s.DefaultDataLimitMb = r.DefaultDataLimitMb;
        s.TimeZoneId = r.TimeZoneId.Trim();
        Log(organizationId, "OrganizationSettingsUpdated", nameof(OrganizationSettings), s.Id.ToString());
        await _db.SaveChangesAsync(ct);
        return ToDto(s);
    }

    private static OrganizationSettingsDto ToDto(OrganizationSettings s) =>
        new(s.OrganizationId, s.OtpExpirySeconds, s.OtpResendCooldownSeconds, s.OtpMaxAttempts, s.SessionValiditySeconds,
            s.DefaultMaxDevices, s.DefaultDataLimitMb, s.TimeZoneId);

    // ---------------------------------------------------------------- branding

    public async Task<OrganizationBrandingDto> GetBrandingAsync(Guid organizationId, CancellationToken ct = default)
    {
        _tenant.EnsureOrganization(organizationId);
        var b = await _db.OrganizationBranding.AsNoTracking().FirstOrDefaultAsync(x => x.OrganizationId == organizationId, ct)
                ?? throw new NotFoundException("Organization branding not found.");
        return ToDto(b);
    }

    public async Task<OrganizationBrandingDto> UpdateBrandingAsync(Guid organizationId, UpdateOrganizationBrandingRequest r, CancellationToken ct = default)
    {
        EnsureOrgWide(organizationId);
        var logo = NormalizeHttpsUrl(r.LogoUrl, "Logo URL");
        var bg = NormalizeHttpsUrl(r.BackgroundUrl, "Background URL");

        var b = await _db.OrganizationBranding.FirstOrDefaultAsync(x => x.OrganizationId == organizationId, ct)
                ?? throw new NotFoundException("Organization branding not found.");
        b.LogoUrl = logo;
        b.BackgroundUrl = bg;
        b.WelcomeMessage = string.IsNullOrWhiteSpace(r.WelcomeMessage) ? null : r.WelcomeMessage.Trim();
        b.ThemeColor = r.ThemeColor;
        b.TermsAndConditions = string.IsNullOrWhiteSpace(r.TermsAndConditions) ? null : r.TermsAndConditions;
        b.PrivacyPolicy = string.IsNullOrWhiteSpace(r.PrivacyPolicy) ? null : r.PrivacyPolicy;
        Log(organizationId, "OrganizationBrandingUpdated", nameof(OrganizationBranding), b.Id.ToString());
        await _db.SaveChangesAsync(ct);
        return ToDto(b);
    }

    private static OrganizationBrandingDto ToDto(OrganizationBranding b) =>
        new(b.OrganizationId, b.LogoUrl, b.WelcomeMessage, b.ThemeColor, b.BackgroundUrl, b.TermsAndConditions, b.PrivacyPolicy);

    /// <summary>Only absolute https URLs are stored, so branding can never carry javascript: or data: URLs into the captive portal.</summary>
    private static string? NormalizeHttpsUrl(string? value, string label)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new ValidationFailedException($"{label} must be an absolute https URL.");
        return uri.ToString();
    }

    // ---------------------------------------------------------------- login methods

    public async Task<List<LoginMethodDto>> ListLoginMethodsAsync(Guid organizationId, Guid? siteId, bool effective, CancellationToken ct = default)
    {
        _tenant.EnsureOrganization(organizationId);
        if (siteId != null) await AuthorizeSiteInOrgAsync(organizationId, siteId.Value, ct);

        if (effective && siteId != null)
        {
            var own = await QueryMethods(organizationId, siteId).Where(m => m.IsEnabled).ToListAsync(ct);
            if (own.Count > 0) return Order(own);
            // no site override: the organization's methods apply
            var org = await QueryMethods(organizationId, null).Where(m => m.IsEnabled).ToListAsync(ct);
            return Order(org);
        }

        var q = QueryMethods(organizationId, siteId);
        if (effective) q = q.Where(m => m.IsEnabled);
        return Order(await q.ToListAsync(ct));
    }

    private IQueryable<OrganizationLoginMethod> QueryMethods(Guid orgId, Guid? siteId) =>
        _db.OrganizationLoginMethods.AsNoTracking().Where(m => m.OrganizationId == orgId && m.SiteId == siteId);

    private static List<LoginMethodDto> Order(IEnumerable<OrganizationLoginMethod> src) =>
        src.OrderByDescending(m => m.IsDefault).ThenBy(m => m.AuthenticationType.ToString())
            .Select(m => new LoginMethodDto(m.LoginMethodId, m.SiteId, m.AuthenticationType, m.IsEnabled, m.IsDefault,
                m.RequireOTP, m.RequireBookingValidation, m.AllowMultipleDevices, m.MaxDevices)).ToList();

    public async Task<List<LoginMethodDto>> UpsertLoginMethodAsync(Guid organizationId, Guid? siteId, UpsertLoginMethodRequest r, CancellationToken ct = default)
    {
        await AuthorizeScopeForWriteAsync(organizationId, siteId, ct);
        var type = r.AuthenticationType ?? throw new ValidationFailedException("AuthenticationType is required.");
        if (!Enum.IsDefined(type)) throw new ValidationFailedException("Unknown login method.");
        if (r.IsDefault && !r.IsEnabled) throw new ValidationFailedException("A disabled login method cannot be the default.");
        await EnsureOrganizationExistsAsync(organizationId, ct);

        var scope = await _db.OrganizationLoginMethods
            .Where(m => m.OrganizationId == organizationId && m.SiteId == siteId).ToListAsync(ct);

        var m = scope.FirstOrDefault(x => x.AuthenticationType == type);
        if (m == null)
        {
            m = new OrganizationLoginMethod { OrganizationId = organizationId, SiteId = siteId, AuthenticationType = type };
            _db.OrganizationLoginMethods.Add(m);
            scope.Add(m);
        }
        m.IsEnabled = r.IsEnabled;
        m.IsDefault = r.IsDefault;
        m.RequireOTP = r.RequireOTP;
        m.RequireBookingValidation = r.RequireBookingValidation;
        m.AllowMultipleDevices = r.AllowMultipleDevices;
        m.MaxDevices = r.AllowMultipleDevices ? r.MaxDevices : 1;
        m.UpdatedAt = DateTime.UtcNow;

        if (m.IsDefault)
            foreach (var other in scope.Where(x => x != m && x.IsDefault)) other.IsDefault = false;

        // The organization level must always keep at least one enabled method, or nobody could log in.
        if (siteId == null && !scope.Any(x => x.IsEnabled))
            throw new ConflictException("At least one login method must stay enabled for the organization.");
        EnsureDefaultAmongEnabled(scope);

        Log(organizationId, "LoginMethodSaved", nameof(OrganizationLoginMethod), m.LoginMethodId.ToString(),
            $"Type={type}; Site={(siteId?.ToString() ?? "org")}; Enabled={m.IsEnabled}");
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { throw new ConflictException("The login method was changed concurrently. Please retry."); }

        return Order(scope);
    }

    public async Task DeleteLoginMethodAsync(Guid organizationId, Guid loginMethodId, CancellationToken ct = default)
    {
        _tenant.EnsureOrganization(organizationId);
        var m = await _db.OrganizationLoginMethods.FirstOrDefaultAsync(x => x.LoginMethodId == loginMethodId && x.OrganizationId == organizationId, ct)
                ?? throw new NotFoundException("Login method not found.");
        await AuthorizeScopeForWriteAsync(organizationId, m.SiteId, ct);

        var scope = await _db.OrganizationLoginMethods
            .Where(x => x.OrganizationId == organizationId && x.SiteId == m.SiteId && x.LoginMethodId != m.LoginMethodId).ToListAsync(ct);
        if (m.SiteId == null && !scope.Any(x => x.IsEnabled))
            throw new ConflictException("At least one login method must stay enabled for the organization.");

        _db.OrganizationLoginMethods.Remove(m);
        EnsureDefaultAmongEnabled(scope);
        Log(organizationId, "LoginMethodDeleted", nameof(OrganizationLoginMethod), m.LoginMethodId.ToString(), $"Type={m.AuthenticationType}");
        await _db.SaveChangesAsync(ct);
    }

    private static void EnsureDefaultAmongEnabled(List<OrganizationLoginMethod> scope)
    {
        var enabled = scope.Where(x => x.IsEnabled).ToList();
        if (enabled.Count == 0) return;
        if (enabled.Any(x => x.IsDefault)) return;
        foreach (var x in scope.Where(x => x.IsDefault)) x.IsDefault = false;
        enabled.OrderBy(x => x.AuthenticationType.ToString()).First().IsDefault = true;
    }

    // ---------------------------------------------------------------- SMS configuration

    public async Task<List<SmsConfigDto>> ListSmsAsync(Guid organizationId, CancellationToken ct = default)
    {
        _tenant.EnsureOrganization(organizationId);
        var rows = await _db.OrganizationSmsSettings.AsNoTracking().Where(s => s.OrganizationId == organizationId).ToListAsync(ct);

        if (!_current.IsPlatformAdmin && !_current.HasAllSites)
        {
            var allowed = await _db.AdminUserSites.Where(x => x.AdminUserId == _current.AdminUserId).Select(x => x.SiteId).ToListAsync(ct);
            rows = rows.Where(r => r.SiteId != null && allowed.Contains(r.SiteId.Value)).ToList();
        }
        return rows.OrderBy(r => r.SiteId != null).Select(ToDto).ToList();
    }

    public async Task<SmsConfigDto> UpsertSmsAsync(Guid organizationId, Guid? siteId, UpsertSmsConfigRequest r, CancellationToken ct = default)
    {
        await AuthorizeScopeForWriteAsync(organizationId, siteId, ct);
        await EnsureOrganizationExistsAsync(organizationId, ct);

        var row = await _db.OrganizationSmsSettings
            .FirstOrDefaultAsync(s => s.OrganizationId == organizationId && s.SiteId == siteId, ct);
        var isNew = row == null;
        row ??= new OrganizationSmsSetting { OrganizationId = organizationId, SiteId = siteId };
        ApplySms(row, r, isNew);
        if (isNew) _db.OrganizationSmsSettings.Add(row);

        // Never include the key (or the endpoint, which may carry it) in the audit trail.
        Log(organizationId, "SmsConfigSaved", nameof(OrganizationSmsSetting), row.SmsSettingId.ToString(),
            $"Site={(siteId?.ToString() ?? "org")}; Provider={row.ProviderName}; Active={row.IsActive}; KeyChanged={!string.IsNullOrWhiteSpace(r.ApiKey)}");
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { throw new ConflictException("The SMS configuration was changed concurrently. Please retry."); }
        return ToDto(row);
    }

    public async Task DeleteSmsAsync(Guid organizationId, Guid? siteId, CancellationToken ct = default)
    {
        await AuthorizeScopeForWriteAsync(organizationId, siteId, ct);
        var row = await _db.OrganizationSmsSettings
                      .FirstOrDefaultAsync(s => s.OrganizationId == organizationId && s.SiteId == siteId, ct)
                  ?? throw new NotFoundException("SMS configuration not found.");
        _db.OrganizationSmsSettings.Remove(row);
        Log(organizationId, "SmsConfigDeleted", nameof(OrganizationSmsSetting), row.SmsSettingId.ToString(),
            $"Site={(siteId?.ToString() ?? "org")}");
        await _db.SaveChangesAsync(ct);
    }

    public async Task<SmsConfigDto> GetPlatformSmsAsync(CancellationToken ct = default)
    {
        RequirePlatform();
        var row = await _db.OrganizationSmsSettings.AsNoTracking()
                      .FirstOrDefaultAsync(s => s.OrganizationId == null && s.SiteId == null, ct)
                  ?? throw new NotFoundException("No platform SMS configuration exists.");
        return ToDto(row);
    }

    public async Task<SmsConfigDto> UpsertPlatformSmsAsync(UpsertSmsConfigRequest r, CancellationToken ct = default)
    {
        RequirePlatform();
        var row = await _db.OrganizationSmsSettings
            .FirstOrDefaultAsync(s => s.OrganizationId == null && s.SiteId == null, ct);
        var isNew = row == null;
        row ??= new OrganizationSmsSetting();
        ApplySms(row, r, isNew);
        if (isNew) _db.OrganizationSmsSettings.Add(row);
        Log(null, "PlatformSmsConfigSaved", nameof(OrganizationSmsSetting), row.SmsSettingId.ToString(),
            $"Provider={row.ProviderName}; Active={row.IsActive}; KeyChanged={!string.IsNullOrWhiteSpace(r.ApiKey)}");
        await _db.SaveChangesAsync(ct);
        return ToDto(row);
    }

    private void ApplySms(OrganizationSmsSetting row, UpsertSmsConfigRequest r, bool isNew)
    {
        var endpoint = string.IsNullOrWhiteSpace(r.ApiEndpoint)
            ? (isNew || string.IsNullOrEmpty(row.ApiEndpoint) ? _sms.DefaultEndpoint : row.ApiEndpoint)
            : r.ApiEndpoint.Trim();
        ValidateSmsEndpoint(endpoint);

        var template = string.IsNullOrWhiteSpace(r.MessageTemplate)
            ? (isNew || string.IsNullOrEmpty(row.MessageTemplate) ? _sms.DefaultMessageTemplate : row.MessageTemplate)
            : r.MessageTemplate.Trim();
        if (!template.Contains("{OTP}", StringComparison.Ordinal))
            throw new ValidationFailedException("SMS message template must contain the {OTP} placeholder.");

        var key = r.ApiKey?.Trim();
        if (string.IsNullOrEmpty(key))
        {
            if (isNew || string.IsNullOrEmpty(row.ApiKeyEncrypted)) throw new ValidationFailedException("An API key is required.");
        }
        else
        {
            if (key.Length < 8 || key.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)))
                throw new ValidationFailedException("API key format is invalid.");
            row.ApiKeyEncrypted = _protector.Protect(key);
        }

        row.ProviderName = string.IsNullOrWhiteSpace(r.ProviderName) ? (isNew ? "2Factor" : row.ProviderName) : r.ProviderName.Trim();
        row.ApiEndpoint = endpoint;
        row.SenderId = r.SenderId.Trim();
        row.MessageTemplate = template;
        row.IsActive = r.IsActive;
        row.UpdatedAt = DateTime.UtcNow;
    }

    private void ValidateSmsEndpoint(string endpoint)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new ValidationFailedException("SMS endpoint must be an absolute https URL.");
        if (!_sms.EffectiveAllowedHosts.Any(h => string.Equals(h, uri.Host, StringComparison.OrdinalIgnoreCase)))
            throw new ValidationFailedException("SMS endpoint host is not on the allowed list.");
    }

    private static SmsConfigDto ToDto(OrganizationSmsSetting s) =>
        new(s.SmsSettingId, s.OrganizationId, s.SiteId, s.ProviderName, s.ApiEndpoint, s.SenderId, s.MessageTemplate,
            s.IsActive, !string.IsNullOrEmpty(s.ApiKeyEncrypted));

    private void RequirePlatform()
    {
        if (!_current.IsPlatformAdmin) throw new ForbiddenException("Only platform administrators can do this.");
    }

    // ---------------------------------------------------------------- organization types

    public async Task<OrganizationTypeDetailDto> GetTypeAsync(int organizationTypeId, CancellationToken ct = default)
    {
        RequirePlatform();
        var t = await _db.OrganizationTypes.AsNoTracking().Include(x => x.DefaultLoginMethods)
                    .FirstOrDefaultAsync(x => x.OrganizationTypeId == organizationTypeId, ct)
                ?? throw new NotFoundException("Organization type not found.");
        return ToDto(t);
    }

    public async Task<OrganizationTypeDetailDto> CreateTypeAsync(UpsertOrganizationTypeRequest r, CancellationToken ct = default)
    {
        RequirePlatform();
        var name = r.TypeName.Trim();
        ValidateTypeMethods(r.DefaultLoginMethods);
        if (await _db.OrganizationTypes.AnyAsync(t => t.TypeName.ToLower() == name.ToLower(), ct))
            throw new ConflictException("An organization type with this name already exists.");

        var t = new OrganizationType
        {
            TypeName = name,
            Description = string.IsNullOrWhiteSpace(r.Description) ? null : r.Description.Trim(),
            IsActive = r.IsActive
        };
        foreach (var m in r.DefaultLoginMethods) t.DefaultLoginMethods.Add(ToEntity(m));
        _db.OrganizationTypes.Add(t);
        Log(null, "OrganizationTypeCreated", nameof(OrganizationType), null, $"Name={name}");
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { throw new ConflictException("An organization type with this name already exists."); }
        return ToDto(t);
    }

    public async Task<OrganizationTypeDetailDto> UpdateTypeAsync(int organizationTypeId, UpsertOrganizationTypeRequest r, CancellationToken ct = default)
    {
        RequirePlatform();
        var name = r.TypeName.Trim();
        ValidateTypeMethods(r.DefaultLoginMethods);

        var t = await _db.OrganizationTypes.Include(x => x.DefaultLoginMethods)
                    .FirstOrDefaultAsync(x => x.OrganizationTypeId == organizationTypeId, ct)
                ?? throw new NotFoundException("Organization type not found.");
        if (await _db.OrganizationTypes.AnyAsync(x => x.OrganizationTypeId != organizationTypeId && x.TypeName.ToLower() == name.ToLower(), ct))
            throw new ConflictException("An organization type with this name already exists.");

        t.TypeName = name;
        t.Description = string.IsNullOrWhiteSpace(r.Description) ? null : r.Description.Trim();
        t.IsActive = r.IsActive;

        // Defaults are templates only: organizations already registered keep their own copies.
        _db.OrganizationTypeDefaultLoginMethods.RemoveRange(t.DefaultLoginMethods.ToList());
        t.DefaultLoginMethods.Clear();
        foreach (var m in r.DefaultLoginMethods) t.DefaultLoginMethods.Add(ToEntity(m));

        Log(null, "OrganizationTypeUpdated", nameof(OrganizationType), organizationTypeId.ToString(), $"Name={name}; Active={t.IsActive}");
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { throw new ConflictException("An organization type with this name already exists."); }
        return ToDto(t);
    }

    private static void ValidateTypeMethods(List<OrganizationTypeLoginMethodInput> methods)
    {
        if (methods.Count == 0) throw new ValidationFailedException("At least one default login method is required.");
        if (methods.Any(m => m.AuthenticationType == null || !Enum.IsDefined(m.AuthenticationType.Value)))
            throw new ValidationFailedException("Unknown login method.");
        if (methods.Select(m => m.AuthenticationType).Distinct().Count() != methods.Count)
            throw new ValidationFailedException("Duplicate login methods supplied.");
        if (methods.Count(m => m.IsDefault) != 1)
            throw new ValidationFailedException("Exactly one default login method is required.");
    }

    private static OrganizationTypeDefaultLoginMethod ToEntity(OrganizationTypeLoginMethodInput m) => new()
    {
        AuthenticationType = m.AuthenticationType!.Value,
        IsDefault = m.IsDefault,
        RequireOTP = m.RequireOTP,
        RequireBookingValidation = m.RequireBookingValidation,
        AllowMultipleDevices = m.AllowMultipleDevices,
        MaxDevices = m.AllowMultipleDevices ? m.MaxDevices : 1
    };

    private static OrganizationTypeDetailDto ToDto(OrganizationType t) =>
        new(t.OrganizationTypeId, t.TypeName, t.Description, t.IsActive,
            t.DefaultLoginMethods.OrderByDescending(m => m.IsDefault).ThenBy(m => m.AuthenticationType.ToString())
                .Select(m => new OrganizationTypeLoginMethodDto(m.AuthenticationType, m.IsDefault, m.RequireOTP,
                    m.RequireBookingValidation, m.AllowMultipleDevices, m.MaxDevices)).ToList());
}
