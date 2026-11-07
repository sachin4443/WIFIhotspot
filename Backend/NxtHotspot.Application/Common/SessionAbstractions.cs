using NxtHotspot.Application.Dtos;
using NxtHotspot.Domain.Enums;

namespace NxtHotspot.Application.Common;

public interface ISessionService
{
    /// <summary>Platform admins may omit the organization (all tenants); everyone else is limited to their organization and sites.</summary>
    Task<PagedResult<SessionDto>> ListAsync(SessionQuery query, CancellationToken ct = default);
    Task<DisconnectCommandDto> DisconnectAsync(Guid sessionId, CancellationToken ct = default);
    Task<List<DisconnectCommandDto>> RecentDisconnectsAsync(Guid? organizationId, CancellationToken ct = default);
    Task<FileResultDto> ExportAsync(SessionQuery query, CancellationToken ct = default);
}

public interface IUsageReportService
{
    Task<UsageReportDto> GetAsync(Guid? organizationId, Guid? siteId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
}

public interface IRadiusMonitorService
{
    Task<RadiusOverviewDto> OverviewAsync(Guid? organizationId, CancellationToken ct = default);
    Task<PagedResult<RadiusRequestLogDto>> RequestLogsAsync(Guid? organizationId, RadiusResult? result, string? search, DateTime? fromUtc,
        int page, int pageSize, CancellationToken ct = default);
}
