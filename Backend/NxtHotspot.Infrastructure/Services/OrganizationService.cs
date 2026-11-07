using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NxtHotspot.Application.Common;
using NxtHotspot.Application.Dtos;
using NxtHotspot.Domain.Entities;
using NxtHotspot.Domain.Enums;
using NxtHotspot.Infrastructure.Persistence;
using NxtHotspot.Infrastructure.Sms;

namespace NxtHotspot.Infrastructure.Services;

public sealed class OrganizationService : IOrganizationService
{
    private readonly NxtDbContext _db;
    private readonly IPasswordService _passwords;
    private readonly ISecretProtector _protector;
    private readonly ICurrentUser _current;
    private readonly ITenantAccess _tenant;
    private readonly SmsOptions _sms;

    public OrganizationService(NxtDbContext db, IPasswordService passwords, ISecretProtector protector,
        ICurrentUser current, ITenantAccess tenant, IOptions<SmsOptions> sms)
    {
        _db = db; _passwords = passwords; _protector = protector; _current = current; _tenant = tenant; _sms = sms.Value;
    }

    public async Task<OrganizationDetailDto> RegisterAsync(RegisterOrganizationRequest req, CancellationToken ct = default)
    {
        if (!_current.IsPlatformAdmin) throw new ForbiddenException("Only platform administrators can register organizations.");

        var type = await _db.OrganizationTypes.Include(t => t.DefaultLoginMethods)
            .FirstOrDefaultAsync(t => t.OrganizationTypeId == req.OrganizationTypeId, ct);
        if (type == null || !type.IsActive) throw new ValidationFailedException("Organization type is invalid or inactive.");

        if (req.SubscriptionPlanId is { } planId &&
            !await _db.SubscriptionPlans.AnyAsync(p => p.Id == planId && p.IsActive, ct))
            throw new ValidationFailedException("Subscription plan is invalid or inactive.");

        if (req.InitialWalletBalance < 0)
            throw new ValidationFailedException("Initial wallet balance cannot be negative.");

        var passwordError = PasswordPolicy.Validate(req.AdminPassword);
        if (passwordError != null) throw new ValidationFailedException(passwordError);

        var username = req.AdminUsername.Trim();
        var normalized = username.ToUpperInvariant();
        if (await _db.AdminUsers.AnyAsync(u => u.NormalizedUsername == normalized, ct))
            throw new ConflictException("Admin username is already taken.");

        var orgAdminRole = await _db.Roles.FirstOrDefaultAsync(r => r.OrganizationId == null && r.Name == SystemRoles.OrgAdmin, ct)
                           ?? throw new InvalidOperationException("System roles are not seeded. Run the database seeder.");

        var status = req.Status ?? OrganizationStatus.Active;
        var org = new Organization
        {
            Name = req.OrganizationName.Trim(),
            OrganizationTypeId = type.OrganizationTypeId,
            SubscriptionPlanId = req.SubscriptionPlanId,
            ContactPerson = req.ContactPerson.Trim(),
            MobileNumber = req.MobileNumber.Trim(),
            Email = req.Email.Trim(),
            Address = req.Address?.Trim(),
            Status = status
        };
        _db.Organizations.Add(org);

        var settings = new OrganizationSettings { OrganizationId = org.Id };
        _db.OrganizationSettings.Add(settings);
        _db.OrganizationBranding.Add(new OrganizationBranding
        {
            OrganizationId = org.Id,
            LogoUrl = string.IsNullOrWhiteSpace(req.LogoUrl) ? null : req.LogoUrl.Trim(),
            WelcomeMessage = $"Welcome to {org.Name} WiFi"
        });

        var site = new Site
        {
            OrganizationId = org.Id,
            Name = string.IsNullOrWhiteSpace(req.PrimarySiteName) ? "Main Branch" : req.PrimarySiteName.Trim(),
            Code = "MAIN",
            IsPrimary = true
        };
        _db.Sites.Add(site);

        var admin = new AdminUser
        {
            OrganizationId = org.Id,
            Username = username,
            NormalizedUsername = normalized,
            PasswordHash = _passwords.Hash(req.AdminPassword),
            FullName = org.ContactPerson,
            Email = org.Email,
            MobileNumber = org.MobileNumber,
            HasAllSitesAccess = true
        };
        admin.UserRoles.Add(new AdminUserRole { RoleId = orgAdminRole.Id });
        _db.AdminUsers.Add(admin);

        if (req.Sms != null)
        {
            var endpoint = string.IsNullOrWhiteSpace(req.Sms.ApiEndpoint) ? _sms.DefaultEndpoint : req.Sms.ApiEndpoint.Trim();
            ValidateSmsEndpoint(endpoint);
            var template = string.IsNullOrWhiteSpace(req.Sms.MessageTemplate) ? _sms.DefaultMessageTemplate : req.Sms.MessageTemplate.Trim();
            if (!template.Contains("{OTP}", StringComparison.Ordinal))
                throw new ValidationFailedException("SMS message template must contain the {OTP} placeholder.");

            _db.OrganizationSmsSettings.Add(new OrganizationSmsSetting
            {
                OrganizationId = org.Id,
                ProviderName = string.IsNullOrWhiteSpace(req.Sms.ProviderName) ? "2Factor" : req.Sms.ProviderName.Trim(),
                ApiEndpoint = endpoint,
                ApiKeyEncrypted = _protector.Protect(req.Sms.ApiKey),
                SenderId = req.Sms.SenderId.Trim(),
                MessageTemplate = template,
                IsActive = true
            });
        }

        foreach (var m in BuildLoginMethods(org.Id, type, req.DefaultLoginMethods, settings.DefaultMaxDevices))
            _db.OrganizationLoginMethods.Add(m);

        var wallet = new SiteWallet { OrganizationId = org.Id, SiteId = site.Id, Balance = req.InitialWalletBalance };
        _db.SiteWallets.Add(wallet);
        if (req.InitialWalletBalance > 0)
        {
            _db.SiteWalletTransactions.Add(new SiteWalletTransaction
            {
                OrganizationId = org.Id,
                SiteId = site.Id,
                SiteWalletId = wallet.Id,
                Type = WalletTransactionType.Credit,
                Amount = req.InitialWalletBalance,
                BalanceAfter = req.InitialWalletBalance,
                Reference = "INITIAL_BALANCE",
                Description = "Initial balance at registration",
                CreatedByAdminUserId = _current.AdminUserId
            });
        }

        _db.AuditLogs.Add(Audit.Entry(org.Id, _current.AdminUserId, "OrganizationRegistered", nameof(Organization), org.Id.ToString(),
            $"Type={type.TypeName}; Status={status}"));

        try
        {
            await _db.SaveChangesAsync(ct); // single save: everything or nothing
        }
        catch (DbUpdateException)
        {
            throw new ConflictException("Registration failed because a conflicting record exists (for example the admin username).");
        }

        return await BuildDetailAsync(org.Id, ct);
    }

    public async Task<PagedResult<OrganizationSummaryDto>> ListAsync(int page, int pageSize, string? search, CancellationToken ct = default,
        OrganizationListFilter? filter = null)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var q = _db.Organizations.AsNoTracking().AsQueryable();
        if (!_current.IsPlatformAdmin)
        {
            var orgId = _current.OrganizationId ?? throw new ForbiddenException();
            q = q.Where(o => o.Id == orgId);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            q = q.Where(o => o.Name.ToLower().Contains(s) || o.Email.ToLower().Contains(s) || o.ContactPerson.ToLower().Contains(s));
        }
        if (filter?.Status is { } status) q = q.Where(o => o.Status == status);
        if (filter?.OrganizationTypeId is { } typeId) q = q.Where(o => o.OrganizationTypeId == typeId);

        var total = await q.CountAsync(ct);
        var items = await q.OrderBy(o => o.Name).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(o => new OrganizationSummaryDto(o.Id, o.Name, o.OrganizationType!.TypeName, o.Status, o.Sites.Count, o.CreatedAt))
            .ToListAsync(ct);
        return new PagedResult<OrganizationSummaryDto>(items, page, pageSize, total);
    }

    public async Task<OrganizationDetailDto> GetAsync(Guid organizationId, CancellationToken ct = default)
    {
        _tenant.EnsureOrganization(organizationId);
        return await BuildDetailAsync(organizationId, ct);
    }

    public async Task SetStatusAsync(Guid organizationId, OrganizationStatus status, CancellationToken ct = default)
    {
        if (!_current.IsPlatformAdmin) throw new ForbiddenException();
        var org = await _db.Organizations.FirstOrDefaultAsync(o => o.Id == organizationId, ct)
                  ?? throw new NotFoundException("Organization not found.");
        var old = org.Status;
        org.Status = status;
        _db.AuditLogs.Add(Audit.Entry(org.Id, _current.AdminUserId, "OrganizationStatusChanged", nameof(Organization),
            org.Id.ToString(), $"{old} -> {status}"));
        await _db.SaveChangesAsync(ct);
    }

    public async Task<List<OrganizationTypeDto>> ListTypesAsync(bool includeInactive, CancellationToken ct = default)
    {
        var q = _db.OrganizationTypes.AsNoTracking().AsQueryable();
        if (!(includeInactive && _current.IsPlatformAdmin)) q = q.Where(t => t.IsActive);
        return await q.OrderBy(t => t.OrganizationTypeId)
            .Select(t => new OrganizationTypeDto(t.OrganizationTypeId, t.TypeName, t.Description, t.IsActive)).ToListAsync(ct);
    }

    public async Task<List<SubscriptionPlanDto>> ListPlansAsync(CancellationToken ct = default) =>
        await _db.SubscriptionPlans.AsNoTracking().Where(p => p.IsActive).OrderBy(p => p.Name)
            .Select(p => new SubscriptionPlanDto(p.Id, p.Name, p.MonthlyPrice, p.MaxSites, p.MaxNasDevices, p.MaxUsers, p.IsActive))
            .ToListAsync(ct);

    // ---------- helpers ----------

    private void ValidateSmsEndpoint(string endpoint)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new ValidationFailedException("SMS endpoint must be an absolute https URL.");
        if (!_sms.EffectiveAllowedHosts.Any(h => string.Equals(h, uri.Host, StringComparison.OrdinalIgnoreCase)))
            throw new ValidationFailedException("SMS endpoint host is not on the allowed list.");
    }

    private static List<OrganizationLoginMethod> BuildLoginMethods(Guid orgId, OrganizationType type,
        List<AuthenticationType>? requested, int defaultMaxDevices)
    {
        var result = new List<OrganizationLoginMethod>();

        if (requested is { Count: > 0 })
        {
            if (requested.Any(r => !Enum.IsDefined(r))) throw new ValidationFailedException("Unknown login method.");
            if (requested.Distinct().Count() != requested.Count) throw new ValidationFailedException("Duplicate login methods supplied.");

            for (var i = 0; i < requested.Count; i++)
            {
                var template = type.DefaultLoginMethods.FirstOrDefault(d => d.AuthenticationType == requested[i]);
                var (otp, booking) = template != null
                    ? (template.RequireOTP, template.RequireBookingValidation)
                    : LoginMethodDefaults.FlagsFor(requested[i]);
                result.Add(new OrganizationLoginMethod
                {
                    OrganizationId = orgId,
                    AuthenticationType = requested[i],
                    IsDefault = i == 0,
                    RequireOTP = otp,
                    RequireBookingValidation = booking,
                    AllowMultipleDevices = template?.AllowMultipleDevices ?? true,
                    MaxDevices = template?.MaxDevices ?? defaultMaxDevices
                });
            }
            return result;
        }

        if (type.DefaultLoginMethods.Count == 0)
            throw new ValidationFailedException("This organization type has no default login methods; specify DefaultLoginMethods.");

        foreach (var d in type.DefaultLoginMethods)
        {
            result.Add(new OrganizationLoginMethod
            {
                OrganizationId = orgId,
                AuthenticationType = d.AuthenticationType,
                IsDefault = d.IsDefault,
                RequireOTP = d.RequireOTP,
                RequireBookingValidation = d.RequireBookingValidation,
                AllowMultipleDevices = d.AllowMultipleDevices,
                MaxDevices = d.MaxDevices
            });
        }
        return result;
    }

    private async Task<OrganizationDetailDto> BuildDetailAsync(Guid id, CancellationToken ct)
    {
        var org = await _db.Organizations.AsNoTracking().Include(o => o.OrganizationType)
                      .FirstOrDefaultAsync(o => o.Id == id, ct)
                  ?? throw new NotFoundException("Organization not found.");

        var site = await _db.Sites.AsNoTracking().FirstAsync(s => s.OrganizationId == id && s.IsPrimary, ct);
        var wallet = await _db.SiteWallets.AsNoTracking().FirstAsync(w => w.SiteId == site.Id, ct);
        var admin = await _db.AdminUsers.AsNoTracking().Where(u => u.OrganizationId == id).OrderBy(u => u.CreatedAt).FirstOrDefaultAsync(ct);
        var methods = await _db.OrganizationLoginMethods.AsNoTracking().Where(m => m.OrganizationId == id)
            .OrderByDescending(m => m.IsDefault).ThenBy(m => m.AuthenticationType)
            .Select(m => new LoginMethodDto(m.LoginMethodId, m.SiteId, m.AuthenticationType, m.IsEnabled, m.IsDefault,
                m.RequireOTP, m.RequireBookingValidation, m.AllowMultipleDevices, m.MaxDevices))
            .ToListAsync(ct);

        var orgSms = await _db.OrganizationSmsSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.OrganizationId == id && s.SiteId == null && s.IsActive, ct);
        var platformSms = orgSms == null &&
                          await _db.OrganizationSmsSettings.AnyAsync(s => s.OrganizationId == null && s.SiteId == null && s.IsActive, ct);
        var sms = new SmsSettingDto(orgSms != null || platformSms, orgSms?.ProviderName, orgSms?.ApiEndpoint, orgSms?.SenderId,
            orgSms != null && !string.IsNullOrEmpty(orgSms.ApiKeyEncrypted), platformSms);

        return new OrganizationDetailDto(org.Id, org.Name, org.OrganizationTypeId, org.OrganizationType!.TypeName, org.Status,
            org.ContactPerson, org.MobileNumber, org.Email, org.Address, org.SubscriptionPlanId, site.Id, admin?.Id, admin?.Username,
            wallet.Id, wallet.Balance, methods, sms, org.CreatedAt);
    }
}
