using System.ComponentModel.DataAnnotations;
using NxtHotspot.Domain.Enums;

namespace NxtHotspot.Application.Dtos;

// ---------- NAS devices (shared secrets are write-only) ----------
public sealed record NasDto(Guid Id, Guid OrganizationId, Guid SiteId, string SiteName, string Name, string IpAddress, NasVendor Vendor,
    string? RouterOsVersion, int AuthPort, int AcctPort, int CoaPort, string? Description, bool IsActive, bool HasSharedSecret,
    bool HasPortalKey, DateTime? LastSeenAt, int ActiveSessions, DateTime CreatedAt);

public sealed class UpsertNasRequest
{
    [Required] public Guid? SiteId { get; set; }
    [Required, StringLength(150, MinimumLength = 2)] public string Name { get; set; } = "";
    [Required, StringLength(45)] public string IpAddress { get; set; } = "";
    public NasVendor Vendor { get; set; } = NasVendor.MikroTik;
    [StringLength(20)] public string? RouterOsVersion { get; set; }
    /// <summary>Required on create (12–128 characters). On update leave empty to keep the stored secret.</summary>
    [StringLength(128)] public string? SharedSecret { get; set; }
    [Range(1, 65535)] public int AuthPort { get; set; } = 1812;
    [Range(1, 65535)] public int AcctPort { get; set; } = 1813;
    [Range(1, 65535)] public int CoaPort { get; set; } = 3799;
    [StringLength(500)] public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Returned once; the key is stored hashed and cannot be shown again.</summary>
public sealed record PortalKeyDto(Guid NasId, string PortalKey, string PortalUrl);

public sealed record RouterConfigDto(Guid NasId, string RouterOsVersion, string Script, IReadOnlyList<string> Notes);

// ---------- Internet packages ----------
public sealed record InternetPackageDto(Guid Id, Guid OrganizationId, Guid? SiteId, string? SiteName, string Name, string? Description,
    int DownloadKbps, int UploadKbps, long? DataLimitMb, int? ValidityMinutes, int? SessionTimeoutMinutes, int? IdleTimeoutMinutes,
    int MaxDevices, decimal Price, bool IsActive, int SubscriberCount, DateTime CreatedAt);

public sealed class UpsertPackageRequest
{
    public Guid? SiteId { get; set; }
    [Required, StringLength(100, MinimumLength = 2)] public string Name { get; set; } = "";
    [StringLength(500)] public string? Description { get; set; }
    [Range(0, 10_000_000)] public int DownloadKbps { get; set; }
    [Range(0, 10_000_000)] public int UploadKbps { get; set; }
    [Range(1, 100_000_000)] public long? DataLimitMb { get; set; }
    [Range(1, 5_256_000)] public int? ValidityMinutes { get; set; }
    [Range(1, 525_600)] public int? SessionTimeoutMinutes { get; set; }
    [Range(1, 10_080)] public int? IdleTimeoutMinutes { get; set; }
    [Range(1, 50)] public int MaxDevices { get; set; } = 1;
    [Range(0, 10_000_000)] public decimal Price { get; set; }
    public bool IsActive { get; set; } = true;
}

// ---------- Subscriber groups ----------
public sealed record SubscriberGroupDto(Guid Id, Guid OrganizationId, string Name, SubscriberGroupType GroupType, string? Description,
    Guid? DefaultPackageId, string? DefaultPackageName, bool IsActive, int SubscriberCount);

public sealed class UpsertGroupRequest
{
    [Required, StringLength(100, MinimumLength = 1)] public string Name { get; set; } = "";
    public SubscriberGroupType GroupType { get; set; } = SubscriberGroupType.Department;
    [StringLength(500)] public string? Description { get; set; }
    public Guid? DefaultPackageId { get; set; }
    public bool IsActive { get; set; } = true;
}

// ---------- Subscribers ----------
public sealed record SubscriberDto(Guid Id, Guid OrganizationId, Guid? SiteId, string? SiteName, SubscriberType SubscriberType,
    string Identifier, string FullName, string? MobileNumber, string? Email, bool HasPassword, Guid? GroupId, string? GroupName,
    Guid? PackageId, string? PackageName, string? RoomNumber, DateTime? CheckInAt, DateTime? CheckOutAt, DateTime? ValidUntil,
    SubscriberStatus Status, int? MaxDevices, string? Notes, DateTime? LastLoginAt, int ActiveSessions, DateTime CreatedAt);

public sealed class SubscriberQuery
{
    public SubscriberType? Type { get; set; }
    public SubscriberStatus? Status { get; set; }
    public Guid? SiteId { get; set; }
    public Guid? GroupId { get; set; }
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

public sealed class CreateSubscriberRequest
{
    public SubscriberType SubscriberType { get; set; } = SubscriberType.Guest;
    /// <summary>Student / employee ID, username, or mobile for guests. Generated for room guests.</summary>
    [StringLength(100)] public string? Identifier { get; set; }
    [Required, StringLength(150, MinimumLength = 1)] public string FullName { get; set; } = "";
    [RegularExpression(ValidationPatterns.Mobile)] public string? MobileNumber { get; set; }
    [EmailAddress, StringLength(254)] public string? Email { get; set; }
    /// <summary>Optional. Required for password-based login methods. Never returned.</summary>
    [StringLength(128)] public string? Password { get; set; }
    public Guid? SiteId { get; set; }
    public Guid? GroupId { get; set; }
    public Guid? PackageId { get; set; }
    [StringLength(20)] public string? RoomNumber { get; set; }
    public DateTime? CheckInAt { get; set; }
    public DateTime? CheckOutAt { get; set; }
    public DateTime? ValidUntil { get; set; }
    [Range(1, 50)] public int? MaxDevices { get; set; }
    [StringLength(500)] public string? Notes { get; set; }
}

public sealed class UpdateSubscriberRequest
{
    [Required, StringLength(150, MinimumLength = 1)] public string FullName { get; set; } = "";
    [RegularExpression(ValidationPatterns.Mobile)] public string? MobileNumber { get; set; }
    [EmailAddress, StringLength(254)] public string? Email { get; set; }
    /// <summary>Leave empty to keep the current password.</summary>
    [StringLength(128)] public string? NewPassword { get; set; }
    public bool ClearPassword { get; set; }
    public Guid? SiteId { get; set; }
    public Guid? GroupId { get; set; }
    public Guid? PackageId { get; set; }
    [StringLength(20)] public string? RoomNumber { get; set; }
    public DateTime? CheckInAt { get; set; }
    public DateTime? CheckOutAt { get; set; }
    public DateTime? ValidUntil { get; set; }
    [Range(1, 50)] public int? MaxDevices { get; set; }
    [StringLength(500)] public string? Notes { get; set; }
}

public sealed class SetSubscriberStatusRequest { [Required] public SubscriberStatus? Status { get; set; } }

public sealed record ImportRowError(int Row, string Message);
public sealed record ImportResultDto(int Created, int Updated, int Skipped, IReadOnlyList<ImportRowError> Errors);
public sealed record FileResultDto(string FileName, string ContentType, byte[] Content);
