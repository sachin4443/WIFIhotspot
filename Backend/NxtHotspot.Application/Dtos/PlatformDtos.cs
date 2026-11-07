using System.ComponentModel.DataAnnotations;
using NxtHotspot.Domain.Enums;

namespace NxtHotspot.Application.Dtos;

// ---------- Account context (who am I, which organization and sites) ----------
public sealed record AccountOrganizationDto(Guid Id, string Name, int OrganizationTypeId, string OrganizationTypeName, OrganizationStatus Status);
public sealed record AccountSiteDto(Guid Id, string Name, string Code, bool IsPrimary, bool IsActive);
public sealed record AccountContextDto(Guid AdminUserId, string Username, string FullName, bool IsPlatformAdmin, bool HasAllSites,
    AccountOrganizationDto? Organization, IReadOnlyList<AccountSiteDto> Sites, IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions);

// ---------- Dashboards ----------
public sealed record CountPair(int Total, int Active);
public sealed record LabelCount(string Label, int Count);
public sealed record MonthCount(string Month, int Count);
public sealed record WalletSummaryDto(decimal TotalBalance, string Currency, int WalletCount, decimal CreditsLast30Days, decimal DebitsLast30Days, int LowBalanceWallets);

/// <summary>Hotspot figures. All counts come from the database; RADIUS-driven numbers are zero until the RADIUS server records sessions.</summary>
public sealed record HotspotSummaryDto(int NasDevices, int ActiveNasDevices, int Subscribers, int ActiveSubscribers, int Packages,
    int ActiveSessions, long DataUsedBytesLast30Days, int SessionsLast30Days);

public sealed record PlatformDashboardDto(
    int TotalOrganizations, IReadOnlyList<LabelCount> OrganizationsByStatus, IReadOnlyList<LabelCount> OrganizationsByType,
    CountPair Sites, CountPair AdminUsers, CountPair SubscriptionPlans, IReadOnlyList<LabelCount> OrganizationsByPlan,
    WalletSummaryDto Wallet, HotspotSummaryDto Hotspot, IReadOnlyList<MonthCount> RegistrationsByMonth,
    IReadOnlyList<OrganizationSummaryDto> RecentOrganizations, IReadOnlyList<AuditLogDto> RecentActivity);

public sealed record OrganizationStatisticsDto(Guid OrganizationId, string Name, string OrganizationTypeName, OrganizationStatus Status,
    string? SubscriptionPlanName, int? MaxSites, int? MaxNasDevices, int? MaxUsers,
    CountPair Sites, CountPair AdminUsers, WalletSummaryDto Wallet, HotspotSummaryDto Hotspot,
    IReadOnlyList<SiteStatisticsDto> SiteBreakdown, IReadOnlyList<AuditLogDto> RecentActivity);

public sealed record SiteStatisticsDto(Guid SiteId, string Name, string Code, bool IsActive, decimal WalletBalance,
    int NasDevices, int Subscribers, int ActiveSessions);

// ---------- Site directory (cross-organization) ----------
public sealed record SiteListItemDto(Guid Id, Guid OrganizationId, string OrganizationName, string Name, string Code, string? City,
    bool IsPrimary, bool IsActive, decimal WalletBalance, DateTime CreatedAt);

// ---------- Wallet overview ----------
public sealed record WalletOverviewDto(Guid WalletId, Guid SiteId, string SiteName, string SiteCode, Guid OrganizationId,
    string OrganizationName, decimal Balance, string Currency, bool IsActive, DateTime UpdatedAt);

// ---------- Subscription plans (platform management) ----------
public sealed record SubscriptionPlanDetailDto(Guid Id, string Name, string? Description, decimal MonthlyPrice, int? MaxSites,
    int? MaxNasDevices, int? MaxUsers, bool IsActive, int OrganizationCount, DateTime CreatedAt);

public sealed class UpsertSubscriptionPlanRequest
{
    [Required, StringLength(100, MinimumLength = 2)] public string Name { get; set; } = "";
    [StringLength(500)] public string? Description { get; set; }
    [Range(0, 10_000_000)] public decimal MonthlyPrice { get; set; }
    [Range(1, 100_000)] public int? MaxSites { get; set; }
    [Range(1, 100_000)] public int? MaxNasDevices { get; set; }
    [Range(1, 10_000_000)] public int? MaxUsers { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class AssignPlanRequest
{
    /// <summary>Null removes the plan (no limits).</summary>
    public Guid? SubscriptionPlanId { get; set; }
}
