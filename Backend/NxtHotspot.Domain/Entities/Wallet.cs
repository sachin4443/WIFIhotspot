using NxtHotspot.Domain.Common;
using NxtHotspot.Domain.Enums;

namespace NxtHotspot.Domain.Entities;

public class SiteWallet : BaseEntity
{
    public Guid OrganizationId { get; set; }
    public Guid SiteId { get; set; }
    public Site? Site { get; set; }
    public decimal Balance { get; set; }
    public string Currency { get; set; } = "INR";
    public bool IsActive { get; set; } = true;
    /// <summary>Optimistic concurrency token, incremented on every balance change.</summary>
    public long Version { get; set; }
}

/// <summary>Append-only ledger row. Never updated or deleted.</summary>
public class SiteWalletTransaction : BaseEntity
{
    public Guid OrganizationId { get; set; }
    public Guid SiteId { get; set; }
    public Guid SiteWalletId { get; set; }
    public SiteWallet? SiteWallet { get; set; }
    public WalletTransactionType Type { get; set; }
    public decimal Amount { get; set; }
    public decimal BalanceAfter { get; set; }
    public string? Reference { get; set; }
    public string? Description { get; set; }
    public Guid? CreatedByAdminUserId { get; set; }
    /// <summary>Caller-supplied key making a retry return the original transaction instead of applying it twice.</summary>
    public string? IdempotencyKey { get; set; }
}

public class AuditLog : BaseEntity
{
    public Guid? OrganizationId { get; set; }
    public Guid? AdminUserId { get; set; }
    public string Action { get; set; } = "";
    public string EntityType { get; set; } = "";
    public string? EntityId { get; set; }
    public string? Details { get; set; }
    public string? IpAddress { get; set; }
}
