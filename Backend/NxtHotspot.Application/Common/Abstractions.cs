using NxtHotspot.Application.Dtos;
using NxtHotspot.Domain.Entities;
using NxtHotspot.Domain.Enums;

namespace NxtHotspot.Application.Common;

public interface ICurrentUser
{
    Guid? AdminUserId { get; }
    /// <summary>Null for platform administrators.</summary>
    Guid? OrganizationId { get; }
    bool IsPlatformAdmin { get; }
    bool HasAllSites { get; }
    IReadOnlySet<string> Permissions { get; }
}

public interface ISecretProtector
{
    string Protect(string plaintext);
    string Unprotect(string protectedValue);
}

public interface IPasswordService
{
    string Hash(string password);
    bool Verify(string hash, string password);
    /// <summary>Spends comparable time to a real verification so unknown usernames are not distinguishable by timing.</summary>
    void VerifyDummy(string password);
}

public sealed record TokenResult(string AccessToken, DateTime ExpiresAtUtc);

public interface ITokenService
{
    TokenResult Create(AdminUser user, IReadOnlyCollection<string> roles, IReadOnlyCollection<string> permissions);
}

public interface ITenantAccess
{
    /// <summary>Throws Forbidden unless the caller may act inside this organization.</summary>
    void EnsureOrganization(Guid organizationId);
    /// <summary>Loads the site and verifies organization and site-level access. Throws NotFound/Forbidden.</summary>
    Task<Site> GetAuthorizedSiteAsync(Guid siteId, CancellationToken ct = default);
}

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, string? ipAddress, CancellationToken ct = default);
}

public interface IOrganizationService
{
    Task<OrganizationDetailDto> RegisterAsync(RegisterOrganizationRequest request, CancellationToken ct = default);
    Task<PagedResult<OrganizationSummaryDto>> ListAsync(int page, int pageSize, string? search, CancellationToken ct = default);
    Task<OrganizationDetailDto> GetAsync(Guid organizationId, CancellationToken ct = default);
    Task SetStatusAsync(Guid organizationId, OrganizationStatus status, CancellationToken ct = default);
    Task<List<OrganizationTypeDto>> ListTypesAsync(bool includeInactive, CancellationToken ct = default);
    Task<List<SubscriptionPlanDto>> ListPlansAsync(CancellationToken ct = default);
}

public interface ISiteService
{
    Task<List<SiteDto>> ListAsync(Guid organizationId, CancellationToken ct = default);
    Task<SiteDto> CreateAsync(Guid organizationId, CreateSiteRequest request, CancellationToken ct = default);
    Task<SiteDto> UpdateAsync(Guid siteId, UpdateSiteRequest request, CancellationToken ct = default);
}

public interface IAdminUserService
{
    Task<List<AdminUserDto>> ListAsync(Guid? organizationId, CancellationToken ct = default);
    Task<AdminUserDto> CreateAsync(CreateAdminUserRequest request, CancellationToken ct = default);
    Task<AdminUserDto> SetRolesAsync(Guid adminUserId, IReadOnlyCollection<Guid> roleIds, CancellationToken ct = default);
    Task<AdminUserDto> SetSitesAsync(Guid adminUserId, IReadOnlyCollection<Guid> siteIds, bool hasAllSites, CancellationToken ct = default);
    Task SetActiveAsync(Guid adminUserId, bool isActive, CancellationToken ct = default);
}

public interface IRoleService
{
    Task<List<RoleDto>> ListRolesAsync(CancellationToken ct = default);
    Task<List<PermissionDto>> ListPermissionsAsync(CancellationToken ct = default);
    Task<RoleDto> CreateRoleAsync(CreateRoleRequest request, CancellationToken ct = default);
    Task<RoleDto> SetRolePermissionsAsync(Guid roleId, IReadOnlyCollection<string> permissionCodes, CancellationToken ct = default);
    Task DeleteRoleAsync(Guid roleId, CancellationToken ct = default);
}

/// <summary>Core ledger operations. No caller identity checks: API controllers must authorize the site first.</summary>
public interface IWalletService
{
    Task<WalletDto> GetAsync(Guid siteId, CancellationToken ct = default);
    Task<WalletTransactionDto> CreditAsync(Guid siteId, decimal amount, string? reference, string? description, string? idempotencyKey, Guid? adminUserId, CancellationToken ct = default);
    Task<WalletTransactionDto> DebitAsync(Guid siteId, decimal amount, string? reference, string? description, string? idempotencyKey, Guid? adminUserId, CancellationToken ct = default);
    Task<PagedResult<WalletTransactionDto>> TransactionsAsync(Guid siteId, int page, int pageSize, CancellationToken ct = default);
}

public interface ISmsSettingResolver
{
    /// <summary>Site override, then organization setting, then platform default. Never mixes tenants. Null if none active.</summary>
    Task<OrganizationSmsSetting?> ResolveAsync(Guid organizationId, Guid? siteId, CancellationToken ct = default);
}
