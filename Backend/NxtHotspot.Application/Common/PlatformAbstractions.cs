using NxtHotspot.Application.Dtos;
using NxtHotspot.Domain.Enums;

namespace NxtHotspot.Application.Common;

public interface IAccountService
{
    Task<AccountContextDto> GetContextAsync(CancellationToken ct = default);
}

public interface IDashboardService
{
    /// <summary>Platform-wide figures. Platform administrators only.</summary>
    Task<PlatformDashboardDto> GetPlatformAsync(CancellationToken ct = default);
    /// <summary>One organization; site-limited admins only see their own sites' figures.</summary>
    Task<OrganizationStatisticsDto> GetOrganizationAsync(Guid organizationId, CancellationToken ct = default);
}

public interface ISiteDirectoryService
{
    Task<PagedResult<SiteListItemDto>> ListAsync(Guid? organizationId, string? search, bool? isActive, int page, int pageSize, CancellationToken ct = default);
    Task<PagedResult<WalletOverviewDto>> ListWalletsAsync(Guid? organizationId, string? search, int page, int pageSize, CancellationToken ct = default);
}

public interface IPlanService
{
    Task<List<SubscriptionPlanDetailDto>> ListAsync(CancellationToken ct = default);
    Task<SubscriptionPlanDetailDto> CreateAsync(UpsertSubscriptionPlanRequest request, CancellationToken ct = default);
    Task<SubscriptionPlanDetailDto> UpdateAsync(Guid planId, UpsertSubscriptionPlanRequest request, CancellationToken ct = default);
    Task AssignAsync(Guid organizationId, Guid? planId, CancellationToken ct = default);
}

/// <summary>Filters for organization listing.</summary>
public sealed record OrganizationListFilter(OrganizationStatus? Status, int? OrganizationTypeId);
