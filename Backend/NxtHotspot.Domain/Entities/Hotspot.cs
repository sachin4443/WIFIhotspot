using NxtHotspot.Domain.Common;
using NxtHotspot.Domain.Enums;

namespace NxtHotspot.Domain.Entities;

/// <summary>A router (MikroTik hotspot) that talks RADIUS to NXT. Identified by its source IP; the shared secret is per device.</summary>
public class NasDevice : BaseEntity
{
    public Guid OrganizationId { get; set; }
    public Guid SiteId { get; set; }
    public Site? Site { get; set; }
    public string Name { get; set; } = "";
    /// <summary>Source IP the RADIUS packets arrive from. Unique across the platform.</summary>
    public string IpAddress { get; set; } = "";
    public NasVendor Vendor { get; set; } = NasVendor.MikroTik;
    /// <summary>e.g. "v6" or "v7" for MikroTik RouterOS.</summary>
    public string? RouterOsVersion { get; set; }
    /// <summary>AES-GCM protected. Never returned by any API, never logged.</summary>
    public string SharedSecretEncrypted { get; set; } = "";
    public int AuthPort { get; set; } = 1812;
    public int AcctPort { get; set; } = 1813;
    public int CoaPort { get; set; } = 3799;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    /// <summary>SHA-256 of the captive-portal key configured on this router (key shown once).</summary>
    public string? PortalKeyHash { get; set; }
    public DateTime? LastSeenAt { get; set; }
}

/// <summary>Internet package / plan for subscribers: bandwidth, duration, quota and device limits.</summary>
public class InternetPackage : BaseEntity
{
    public Guid OrganizationId { get; set; }
    /// <summary>Null = available at every site.</summary>
    public Guid? SiteId { get; set; }
    public Site? Site { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    /// <summary>0 = unlimited.</summary>
    public int DownloadKbps { get; set; }
    public int UploadKbps { get; set; }
    /// <summary>Null = unlimited.</summary>
    public long? DataLimitMb { get; set; }
    /// <summary>Access validity from first sign-in. Null = no time limit.</summary>
    public int? ValidityMinutes { get; set; }
    /// <summary>Maximum length of one RADIUS session (Session-Timeout).</summary>
    public int? SessionTimeoutMinutes { get; set; }
    public int? IdleTimeoutMinutes { get; set; }
    public int MaxDevices { get; set; } = 1;
    public decimal Price { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Department, class, course, batch or floor used to group subscribers and apply a default package.</summary>
public class SubscriberGroup : BaseEntity
{
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = "";
    public SubscriberGroupType GroupType { get; set; } = SubscriberGroupType.Department;
    public string? Description { get; set; }
    public Guid? DefaultPackageId { get; set; }
    public InternetPackage? DefaultPackage { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>A hotspot end user: guest, hotel room guest, student, teacher, staff, employee or ISP customer.</summary>
public class Subscriber : BaseEntity
{
    public Guid OrganizationId { get; set; }
    /// <summary>Null = valid at every site of the organization.</summary>
    public Guid? SiteId { get; set; }
    public Site? Site { get; set; }
    public SubscriberType SubscriberType { get; set; }
    /// <summary>Username / student ID / employee ID / mobile number. Unique per organization (case-insensitive).</summary>
    public string Identifier { get; set; } = "";
    public string NormalizedIdentifier { get; set; } = "";
    public string FullName { get; set; } = "";
    public string? MobileNumber { get; set; }
    public string? Email { get; set; }
    /// <summary>PBKDF2 hash. Null for OTP-only subscribers.</summary>
    public string? PasswordHash { get; set; }
    public Guid? GroupId { get; set; }
    public SubscriberGroup? Group { get; set; }
    public Guid? PackageId { get; set; }
    public InternetPackage? Package { get; set; }
    /// <summary>Hotel room; scoped by SiteId, never a global username.</summary>
    public string? RoomNumber { get; set; }
    public DateTime? CheckInAt { get; set; }
    public DateTime? CheckOutAt { get; set; }
    public DateTime? ValidUntil { get; set; }
    public SubscriberStatus Status { get; set; } = SubscriberStatus.Active;
    /// <summary>Overrides the package device limit when set.</summary>
    public int? MaxDevices { get; set; }
    public string? Notes { get; set; }
    public DateTime? FirstLoginAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}

/// <summary>Short-lived single-use RADIUS credential issued after a successful captive-portal sign-in.</summary>
public class RadiusCredential
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid SiteId { get; set; }
    public Guid SubscriberId { get; set; }
    public Subscriber? Subscriber { get; set; }
    public Guid? NasId { get; set; }
    public string Username { get; set; } = "";
    /// <summary>SHA-256 of the random one-time password (high entropy, so a fast hash is sufficient).</summary>
    public string PasswordHash { get; set; } = "";
    public string? MacAddress { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class OtpChallenge
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid SiteId { get; set; }
    public OtpPurpose Purpose { get; set; } = OtpPurpose.HotspotLogin;
    public string MobileNumber { get; set; } = "";
    /// <summary>HMAC-SHA256 of the code. Codes are never stored or logged in clear text.</summary>
    public string CodeHash { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public int Attempts { get; set; }
    public DateTime? ConsumedAt { get; set; }
    public string? IpAddress { get; set; }
    public string? MacAddress { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class SmsLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? OrganizationId { get; set; }
    public Guid? SiteId { get; set; }
    /// <summary>Masked, e.g. ******3210.</summary>
    public string MobileMasked { get; set; } = "";
    public string ProviderName { get; set; } = "";
    public bool Success { get; set; }
    public string? Error { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>One accounting session reported by a NAS (Start / Interim-Update / Stop).</summary>
public class RadiusSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid SiteId { get; set; }
    public Guid NasId { get; set; }
    public NasDevice? Nas { get; set; }
    public Guid? SubscriberId { get; set; }
    public Subscriber? Subscriber { get; set; }
    public string Username { get; set; } = "";
    public string AcctSessionId { get; set; } = "";
    public string? FramedIpAddress { get; set; }
    /// <summary>Client MAC (Calling-Station-Id).</summary>
    public string? CallingStationId { get; set; }
    public string? CalledStationId { get; set; }
    public string? NasPortId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime LastUpdateAt { get; set; }
    public DateTime? StoppedAt { get; set; }
    public long SessionTimeSeconds { get; set; }
    public long InputBytes { get; set; }
    public long OutputBytes { get; set; }
    public string? TerminateCause { get; set; }
    public RadiusSessionStatus Status { get; set; } = RadiusSessionStatus.Active;
}

/// <summary>Idempotency record for accounting packets; duplicates are acknowledged but not re-applied.</summary>
public class RadiusAccountingEvent
{
    public long Id { get; set; }
    public Guid NasId { get; set; }
    public string AcctSessionId { get; set; } = "";
    public int StatusType { get; set; }
    public long SessionTime { get; set; }
    public long DelayTime { get; set; }
    public long InputBytes { get; set; }
    public long OutputBytes { get; set; }
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
}

public class RadiusRequestLog
{
    public long Id { get; set; }
    public Guid? OrganizationId { get; set; }
    public Guid? SiteId { get; set; }
    public Guid? NasId { get; set; }
    public string SourceIp { get; set; } = "";
    public RadiusPacketKind PacketKind { get; set; }
    public RadiusResult Result { get; set; }
    public string? Username { get; set; }
    public string? MacAddress { get; set; }
    /// <summary>Never contains passwords or OTPs.</summary>
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Disconnect request queued by the API and executed by the RADIUS server (RFC 5176).</summary>
public class DisconnectCommand
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid SiteId { get; set; }
    public Guid NasId { get; set; }
    public Guid SessionId { get; set; }
    public Guid? RequestedByAdminUserId { get; set; }
    public DisconnectStatus Status { get; set; } = DisconnectStatus.Pending;
    public int Attempts { get; set; }
    public string? Result { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}

/// <summary>Heartbeat and counters written by each running RADIUS server instance.</summary>
public class RadiusServerStatus
{
    public string InstanceId { get; set; } = "";
    public string Version { get; set; } = "";
    public string? HostName { get; set; }
    public int AuthPort { get; set; }
    public int AcctPort { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime LastHeartbeatAt { get; set; }
    public long AuthRequests { get; set; }
    public long AuthAccepts { get; set; }
    public long AuthRejects { get; set; }
    public long AcctRequests { get; set; }
    public long Dropped { get; set; }
}
