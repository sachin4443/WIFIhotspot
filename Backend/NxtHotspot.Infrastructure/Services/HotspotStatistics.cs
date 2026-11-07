using NxtHotspot.Application.Dtos;
using NxtHotspot.Infrastructure.Persistence;

namespace NxtHotspot.Infrastructure.Services;

/// <summary>
/// Hotspot counts (NAS, subscribers, packages, sessions) for dashboards.
/// The hotspot tables arrive with the hotspot domain migration; until then every figure is genuinely zero.
/// </summary>
internal static class HotspotStatistics
{
    public readonly record struct SiteCounts(int Nas, int Subscribers, int ActiveSessions);

    public static Task<HotspotSummaryDto> SummaryAsync(NxtDbContext db, Guid? organizationId, IReadOnlyList<Guid>? siteIds, CancellationToken ct) =>
        Task.FromResult(new HotspotSummaryDto(0, 0, 0, 0, 0, 0, 0, 0));

    public static Task<Dictionary<Guid, SiteCounts>> PerSiteAsync(NxtDbContext db, Guid organizationId, CancellationToken ct) =>
        Task.FromResult(new Dictionary<Guid, SiteCounts>());
}
