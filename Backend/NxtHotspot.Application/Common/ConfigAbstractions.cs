using NxtHotspot.Application.Dtos;

namespace NxtHotspot.Application.Common;

/// <summary>
/// Organization-level configuration: profile, settings, branding, login methods, SMS providers and organization types.
/// Tenant and site scope is enforced here; the API additionally applies permission policies.
/// </summary>
public interface IOrganizationConfigService
{
    Task<OrganizationProfileDto> GetProfileAsync(Guid organizationId, CancellationToken ct = default);
    Task<OrganizationProfileDto> UpdateProfileAsync(Guid organizationId, UpdateOrganizationProfileRequest request, CancellationToken ct = default);

    Task<OrganizationSettingsDto> GetSettingsAsync(Guid organizationId, CancellationToken ct = default);
    Task<OrganizationSettingsDto> UpdateSettingsAsync(Guid organizationId, UpdateOrganizationSettingsRequest request, CancellationToken ct = default);

    Task<OrganizationBrandingDto> GetBrandingAsync(Guid organizationId, CancellationToken ct = default);
    Task<OrganizationBrandingDto> UpdateBrandingAsync(Guid organizationId, UpdateOrganizationBrandingRequest request, CancellationToken ct = default);

    /// <summary>siteId null = organization-level methods. With effective=true and a site, returns the enabled methods that apply to that site (its own overrides, else the organization's).</summary>
    Task<List<LoginMethodDto>> ListLoginMethodsAsync(Guid organizationId, Guid? siteId, bool effective, CancellationToken ct = default);
    Task<List<LoginMethodDto>> UpsertLoginMethodAsync(Guid organizationId, Guid? siteId, UpsertLoginMethodRequest request, CancellationToken ct = default);
    Task DeleteLoginMethodAsync(Guid organizationId, Guid loginMethodId, CancellationToken ct = default);

    Task<List<SmsConfigDto>> ListSmsAsync(Guid organizationId, CancellationToken ct = default);
    Task<SmsConfigDto> UpsertSmsAsync(Guid organizationId, Guid? siteId, UpsertSmsConfigRequest request, CancellationToken ct = default);
    Task DeleteSmsAsync(Guid organizationId, Guid? siteId, CancellationToken ct = default);
    Task<SmsConfigDto> GetPlatformSmsAsync(CancellationToken ct = default);
    Task<SmsConfigDto> UpsertPlatformSmsAsync(UpsertSmsConfigRequest request, CancellationToken ct = default);

    Task<OrganizationTypeDetailDto> GetTypeAsync(int organizationTypeId, CancellationToken ct = default);
    Task<OrganizationTypeDetailDto> CreateTypeAsync(UpsertOrganizationTypeRequest request, CancellationToken ct = default);
    Task<OrganizationTypeDetailDto> UpdateTypeAsync(int organizationTypeId, UpsertOrganizationTypeRequest request, CancellationToken ct = default);
}

public interface IAuditLogService
{
    /// <summary>Organization admins (with access to all sites) see only their own organization; platform admins may filter by organization.</summary>
    Task<PagedResult<AuditLogDto>> ListAsync(Guid? organizationId, string? action, DateTime? fromUtc, DateTime? toUtc,
        int page, int pageSize, CancellationToken ct = default);
}
