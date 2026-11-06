using NxtHotspot.Domain.Common;
using NxtHotspot.Domain.Enums;

namespace NxtHotspot.Domain.Entities;

// NOTE: OrganizationLoginMethod and OrganizationSmsSetting are in Configuration.cs;
// admin/RBAC entities are in Admin.cs; wallet and audit entities are in Wallet.cs.

/// <summary>Master list of business types (Hotel, School, ...). Data, not an enum: new types need no deployment.</summary>
public class OrganizationType
{
    public int OrganizationTypeId { get; set; }
    public string TypeName { get; set; } = "";
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<OrganizationTypeDefaultLoginMethod> DefaultLoginMethods { get; set; } = new List<OrganizationTypeDefaultLoginMethod>();
}

/// <summary>Login methods copied into an organization's own configuration when it is registered with this type.</summary>
public class OrganizationTypeDefaultLoginMethod : BaseEntity
{
    public int OrganizationTypeId { get; set; }
    public OrganizationType? OrganizationType { get; set; }
    public AuthenticationType AuthenticationType { get; set; }
    public bool IsDefault { get; set; }
    public bool RequireOTP { get; set; }
    public bool RequireBookingValidation { get; set; }
    public bool AllowMultipleDevices { get; set; } = true;
    public int MaxDevices { get; set; } = 3;
}

public class SubscriptionPlan : BaseEntity
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public decimal MonthlyPrice { get; set; }
    /// <summary>Null means unlimited.</summary>
    public int? MaxSites { get; set; }
    /// <summary>Null means unlimited.</summary>
    public int? MaxNasDevices { get; set; }
    /// <summary>Null means unlimited.</summary>
    public int? MaxUsers { get; set; }
    public bool IsActive { get; set; } = true;
}

public class Organization : BaseEntity
{
    public string Name { get; set; } = "";
    public int OrganizationTypeId { get; set; }
    public OrganizationType? OrganizationType { get; set; }
    public Guid? SubscriptionPlanId { get; set; }
    public SubscriptionPlan? SubscriptionPlan { get; set; }
    public string ContactPerson { get; set; } = "";
    public string MobileNumber { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Address { get; set; }
    public OrganizationStatus Status { get; set; } = OrganizationStatus.Pending;

    public OrganizationSettings? Settings { get; set; }
    public OrganizationBranding? Branding { get; set; }
    public ICollection<Site> Sites { get; set; } = new List<Site>();
}

public class OrganizationSettings : BaseEntity
{
    public Guid OrganizationId { get; set; }
    public Organization? Organization { get; set; }
    public int OtpExpirySeconds { get; set; } = 300;
    public int OtpResendCooldownSeconds { get; set; } = 60;
    public int OtpMaxAttempts { get; set; } = 5;
    public int SessionValiditySeconds { get; set; } = 86400;
    public int DefaultMaxDevices { get; set; } = 3;
    public int? DefaultDataLimitMb { get; set; }
    public string TimeZoneId { get; set; } = "Asia/Kolkata";
}

public class OrganizationBranding : BaseEntity
{
    public Guid OrganizationId { get; set; }
    public Organization? Organization { get; set; }
    public string? LogoUrl { get; set; }
    public string? WelcomeMessage { get; set; }
    public string ThemeColor { get; set; } = "#0d6efd";
    public string? BackgroundUrl { get; set; }
    public string? TermsAndConditions { get; set; }
    public string? PrivacyPolicy { get; set; }
}

public class Site : BaseEntity
{
    public Guid OrganizationId { get; set; }
    public Organization? Organization { get; set; }
    public string Name { get; set; } = "";
    /// <summary>Stored upper-case; unique per organization.</summary>
    public string Code { get; set; } = "";
    public string? Address { get; set; }
    public string? City { get; set; }
    public bool IsPrimary { get; set; }
    public bool IsActive { get; set; } = true;
}
