using NxtHotspot.Domain.Enums;

namespace NxtHotspot.Domain.Entities;

public class OrganizationLoginMethod
{
    public Guid LoginMethodId { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Organization? Organization { get; set; }
    /// <summary>Null = organization-level; set = site-level override.</summary>
    public Guid? SiteId { get; set; }
    public Site? Site { get; set; }
    public AuthenticationType AuthenticationType { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsDefault { get; set; }
    public bool RequireOTP { get; set; }
    public bool RequireBookingValidation { get; set; }
    public bool AllowMultipleDevices { get; set; } = true;
    public int MaxDevices { get; set; } = 3;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class OrganizationSmsSetting
{
    public Guid SmsSettingId { get; set; } = Guid.NewGuid();
    /// <summary>Null = platform default provider configured by Super Admin.</summary>
    public Guid? OrganizationId { get; set; }
    public Organization? Organization { get; set; }
    public Guid? SiteId { get; set; }
    public Site? Site { get; set; }
    public string ProviderName { get; set; } = "2Factor";
    public string ApiEndpoint { get; set; } = "";
    /// <summary>AES-GCM protected. Never returned by any API, never logged.</summary>
    public string ApiKeyEncrypted { get; set; } = "";
    public string SenderId { get; set; } = "";
    public string MessageTemplate { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
