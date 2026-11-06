using System.ComponentModel.DataAnnotations;
using NxtHotspot.Domain.Enums;

namespace NxtHotspot.Application.Dtos;

// ---------- Organization profile / settings / branding ----------
public sealed class UpdateOrganizationProfileRequest
{
    [Required, StringLength(200, MinimumLength = 2)] public string Name { get; set; } = "";
    [Required, StringLength(150)] public string ContactPerson { get; set; } = "";
    [Required, RegularExpression(ValidationPatterns.Mobile)] public string MobileNumber { get; set; } = "";
    [Required, EmailAddress, StringLength(254)] public string Email { get; set; } = "";
    [StringLength(500)] public string? Address { get; set; }
}

public sealed record OrganizationProfileDto(Guid Id, string Name, string ContactPerson, string MobileNumber, string Email, string? Address);

public sealed record OrganizationSettingsDto(Guid OrganizationId, int OtpExpirySeconds, int OtpResendCooldownSeconds, int OtpMaxAttempts,
    int SessionValiditySeconds, int DefaultMaxDevices, int? DefaultDataLimitMb, string TimeZoneId);

public sealed class UpdateOrganizationSettingsRequest
{
    [Range(30, 3600)] public int OtpExpirySeconds { get; set; } = 300;
    [Range(0, 3600)] public int OtpResendCooldownSeconds { get; set; } = 60;
    [Range(1, 20)] public int OtpMaxAttempts { get; set; } = 5;
    [Range(60, 31_536_000)] public int SessionValiditySeconds { get; set; } = 86400;
    [Range(1, 50)] public int DefaultMaxDevices { get; set; } = 3;
    [Range(1, 10_000_000)] public int? DefaultDataLimitMb { get; set; }
    [Required, StringLength(64)] public string TimeZoneId { get; set; } = "Asia/Kolkata";
}

public sealed record OrganizationBrandingDto(Guid OrganizationId, string? LogoUrl, string? WelcomeMessage, string ThemeColor,
    string? BackgroundUrl, string? TermsAndConditions, string? PrivacyPolicy);

public sealed class UpdateOrganizationBrandingRequest
{
    [StringLength(500)] public string? LogoUrl { get; set; }
    [StringLength(500)] public string? WelcomeMessage { get; set; }
    [RegularExpression(ValidationPatterns.HexColor)] public string ThemeColor { get; set; } = "#0d6efd";
    [StringLength(500)] public string? BackgroundUrl { get; set; }
    [StringLength(20000)] public string? TermsAndConditions { get; set; }
    [StringLength(20000)] public string? PrivacyPolicy { get; set; }
}

// ---------- Login methods ----------
public sealed class UpsertLoginMethodRequest
{
    [Required] public AuthenticationType? AuthenticationType { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsDefault { get; set; }
    public bool RequireOTP { get; set; }
    public bool RequireBookingValidation { get; set; }
    public bool AllowMultipleDevices { get; set; } = true;
    [Range(1, 50)] public int MaxDevices { get; set; } = 3;
}

// ---------- SMS configuration (the API key is write-only) ----------
public sealed record SmsConfigDto(Guid SmsSettingId, Guid? OrganizationId, Guid? SiteId, string ProviderName, string ApiEndpoint,
    string SenderId, string MessageTemplate, bool IsActive, bool HasApiKey);

public sealed class UpsertSmsConfigRequest
{
    [StringLength(50)] public string? ProviderName { get; set; }
    [StringLength(500)] public string? ApiEndpoint { get; set; }
    /// <summary>Required when creating. When updating, omit (or leave blank) to keep the stored key. Never returned by the API.</summary>
    [StringLength(200)] public string? ApiKey { get; set; }
    [Required, StringLength(20, MinimumLength = 3)] public string SenderId { get; set; } = "";
    [StringLength(500)] public string? MessageTemplate { get; set; }
    public bool IsActive { get; set; } = true;
}

// ---------- Organization types ----------
public sealed record OrganizationTypeLoginMethodDto(AuthenticationType AuthenticationType, bool IsDefault, bool RequireOTP,
    bool RequireBookingValidation, bool AllowMultipleDevices, int MaxDevices);

public sealed record OrganizationTypeDetailDto(int OrganizationTypeId, string TypeName, string? Description, bool IsActive,
    IReadOnlyList<OrganizationTypeLoginMethodDto> DefaultLoginMethods);

public sealed class OrganizationTypeLoginMethodInput
{
    [Required] public AuthenticationType? AuthenticationType { get; set; }
    public bool IsDefault { get; set; }
    public bool RequireOTP { get; set; }
    public bool RequireBookingValidation { get; set; }
    public bool AllowMultipleDevices { get; set; } = true;
    [Range(1, 50)] public int MaxDevices { get; set; } = 3;
}

public sealed class UpsertOrganizationTypeRequest
{
    [Required, StringLength(100, MinimumLength = 2)] public string TypeName { get; set; } = "";
    [StringLength(500)] public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    [Required] public List<OrganizationTypeLoginMethodInput> DefaultLoginMethods { get; set; } = new();
}

// ---------- Audit / wallet / account ----------
public sealed record AuditLogDto(Guid Id, Guid? OrganizationId, Guid? AdminUserId, string Action, string EntityType, string? EntityId,
    string? Details, string? IpAddress, DateTime CreatedAt);

public sealed class DebitWalletRequest
{
    [Range(0.01, 10_000_000)] public decimal Amount { get; set; }
    /// <summary>Reason for a manual deduction; recorded on the ledger.</summary>
    [Required, StringLength(100)] public string Reference { get; set; } = "";
    [StringLength(300)] public string? Description { get; set; }
}

public sealed record CurrentUserDto(Guid? AdminUserId, Guid? OrganizationId, bool IsPlatformAdmin, bool HasAllSites,
    IReadOnlyList<string> Permissions);
