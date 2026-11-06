using System.ComponentModel.DataAnnotations;
using NxtHotspot.Domain.Enums;

namespace NxtHotspot.Application.Dtos;

public static class ValidationPatterns
{
    public const string Mobile = @"^\+?[0-9]{10,15}$";
    public const string Username = @"^[a-zA-Z0-9._@-]+$";
    public const string SiteCode = @"^[A-Za-z0-9_-]{2,30}$";
    public const string HexColor = @"^#[0-9a-fA-F]{6}$";
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

// ---------- Auth ----------
public sealed class LoginRequest
{
    [Required, StringLength(50)] public string Username { get; set; } = "";
    [Required, StringLength(128)] public string Password { get; set; } = "";
}

public sealed record LoginResponse(
    string AccessToken, DateTime ExpiresAtUtc, Guid AdminUserId, string Username, string FullName,
    Guid? OrganizationId, bool IsPlatformAdmin, IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions);

// ---------- Organization registration ----------
public sealed class SmsProviderRequest
{
    [StringLength(50)] public string ProviderName { get; set; } = "2Factor";
    [StringLength(500)] public string? ApiEndpoint { get; set; }
    [Required, StringLength(200, MinimumLength = 8)] public string ApiKey { get; set; } = "";
    [Required, StringLength(20, MinimumLength = 3)] public string SenderId { get; set; } = "";
    [StringLength(500)] public string? MessageTemplate { get; set; }
}

public sealed class RegisterOrganizationRequest
{
    [Required, StringLength(200, MinimumLength = 2)] public string OrganizationName { get; set; } = "";
    [Range(1, int.MaxValue)] public int OrganizationTypeId { get; set; }
    [Required, StringLength(150)] public string ContactPerson { get; set; } = "";
    [Required, RegularExpression(ValidationPatterns.Mobile)] public string MobileNumber { get; set; } = "";
    [Required, EmailAddress, StringLength(254)] public string Email { get; set; } = "";
    [StringLength(500)] public string? Address { get; set; }
    [StringLength(150)] public string? PrimarySiteName { get; set; }

    [Required, StringLength(50, MinimumLength = 3), RegularExpression(ValidationPatterns.Username)]
    public string AdminUsername { get; set; } = "";
    [Required, StringLength(128)] public string AdminPassword { get; set; } = "";

    public Guid? SubscriptionPlanId { get; set; }
    [Range(0, 10_000_000)] public decimal InitialWalletBalance { get; set; }

    public SmsProviderRequest? Sms { get; set; }
    /// <summary>When omitted the defaults configured for the organization type are used.</summary>
    public List<AuthenticationType>? DefaultLoginMethods { get; set; }

    [Url, StringLength(500)] public string? LogoUrl { get; set; }
    /// <summary>Defaults to Active.</summary>
    public OrganizationStatus? Status { get; set; }
}

public sealed record LoginMethodDto(Guid LoginMethodId, Guid? SiteId, AuthenticationType AuthenticationType, bool IsEnabled,
    bool IsDefault, bool RequireOTP, bool RequireBookingValidation, bool AllowMultipleDevices, int MaxDevices);

/// <summary>Never includes the API key, only whether one is configured.</summary>
public sealed record SmsSettingDto(bool IsConfigured, string? ProviderName, string? ApiEndpoint, string? SenderId, bool HasApiKey, bool UsesPlatformDefault);

public sealed record OrganizationDetailDto(
    Guid Id, string Name, int OrganizationTypeId, string OrganizationTypeName, OrganizationStatus Status,
    string ContactPerson, string MobileNumber, string Email, string? Address, Guid? SubscriptionPlanId,
    Guid PrimarySiteId, Guid? AdminUserId, string? AdminUsername, Guid WalletId, decimal WalletBalance,
    IReadOnlyList<LoginMethodDto> LoginMethods, SmsSettingDto Sms, DateTime CreatedAt);

public sealed record OrganizationSummaryDto(Guid Id, string Name, string OrganizationTypeName, OrganizationStatus Status,
    int SiteCount, DateTime CreatedAt);

public sealed record OrganizationTypeDto(int OrganizationTypeId, string TypeName, string? Description, bool IsActive);
public sealed record SubscriptionPlanDto(Guid Id, string Name, decimal MonthlyPrice, int? MaxSites, int? MaxNasDevices, int? MaxUsers, bool IsActive);

public sealed class SetOrganizationStatusRequest
{
    [Required] public OrganizationStatus? Status { get; set; }
}

// ---------- Sites ----------
public sealed class CreateSiteRequest
{
    [Required, StringLength(150, MinimumLength = 2)] public string Name { get; set; } = "";
    [Required, RegularExpression(ValidationPatterns.SiteCode)] public string Code { get; set; } = "";
    [StringLength(500)] public string? Address { get; set; }
    [StringLength(100)] public string? City { get; set; }
}

public sealed class UpdateSiteRequest
{
    [Required, StringLength(150, MinimumLength = 2)] public string Name { get; set; } = "";
    [StringLength(500)] public string? Address { get; set; }
    [StringLength(100)] public string? City { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed record SiteDto(Guid Id, Guid OrganizationId, string Name, string Code, string? Address, string? City, bool IsPrimary, bool IsActive);

// ---------- Admin users / roles ----------
public sealed class CreateAdminUserRequest
{
    /// <summary>Null creates a platform user (platform admins only). Ignored for organization admins, who always create inside their own organization.</summary>
    public Guid? OrganizationId { get; set; }
    [Required, StringLength(50, MinimumLength = 3), RegularExpression(ValidationPatterns.Username)] public string Username { get; set; } = "";
    [Required, StringLength(128)] public string Password { get; set; } = "";
    [Required, StringLength(150)] public string FullName { get; set; } = "";
    [Required, EmailAddress, StringLength(254)] public string Email { get; set; } = "";
    [RegularExpression(ValidationPatterns.Mobile)] public string? MobileNumber { get; set; }
    public bool HasAllSitesAccess { get; set; }
    public List<Guid> RoleIds { get; set; } = new();
    public List<Guid> SiteIds { get; set; } = new();
}

public sealed class SetRolesRequest { [Required] public List<Guid> RoleIds { get; set; } = new(); }
public sealed class SetSitesRequest { public bool HasAllSitesAccess { get; set; } public List<Guid> SiteIds { get; set; } = new(); }
public sealed class SetActiveRequest { public bool IsActive { get; set; } }

public sealed record AdminUserDto(Guid Id, Guid? OrganizationId, string Username, string FullName, string Email, string? MobileNumber,
    bool IsActive, bool HasAllSitesAccess, IReadOnlyList<string> Roles, IReadOnlyList<Guid> SiteIds, DateTime? LastLoginAt);

public sealed class CreateRoleRequest
{
    /// <summary>Null creates a system role (platform admins only). Organization admins always create inside their own organization.</summary>
    public Guid? OrganizationId { get; set; }
    [Required, StringLength(80, MinimumLength = 2)] public string Name { get; set; } = "";
    [StringLength(300)] public string? Description { get; set; }
    public List<string> PermissionCodes { get; set; } = new();
}

public sealed class SetRolePermissionsRequest { [Required] public List<string> PermissionCodes { get; set; } = new(); }

public sealed record RoleDto(Guid Id, Guid? OrganizationId, string Name, string? Description, bool IsSystem, bool IsPlatformRole, IReadOnlyList<string> PermissionCodes);
public sealed record PermissionDto(Guid Id, string Code, string? Description, bool IsPlatformOnly);

// ---------- Wallet ----------
public sealed class CreditWalletRequest
{
    [Range(0.01, 10_000_000)] public decimal Amount { get; set; }
    [StringLength(100)] public string? Reference { get; set; }
    [StringLength(300)] public string? Description { get; set; }
}

public sealed record WalletDto(Guid Id, Guid SiteId, Guid OrganizationId, decimal Balance, string Currency, bool IsActive);
public sealed record WalletTransactionDto(Guid Id, Guid SiteWalletId, WalletTransactionType Type, decimal Amount, decimal BalanceAfter,
    string? Reference, string? Description, DateTime CreatedAt);
